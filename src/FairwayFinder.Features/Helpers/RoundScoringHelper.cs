using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// Scoring math shared by the atomic submit path (<c>CreateRoundAsync</c> / <c>UpdateRoundAsync</c>)
/// and the incremental entry path (<c>RoundEntryService</c>). Both write the same columns from the
/// same inputs, so keeping the arithmetic here is what stops the two paths from drifting.
/// </summary>
public static class RoundScoringHelper
{
    /// <summary>One scored hole, reduced to the three fields every calculation below needs.</summary>
    public readonly record struct HoleScoreLine(int HoleNumber, int Score, int Par);

    /// <summary>The round-level score columns, plus the par and hole count actually entered.</summary>
    public readonly record struct RoundTotals(int ScoreOut, int ScoreIn, int Total, int ParEntered, int HoleCount);

    /// <summary>Which holes a round covers, derived from the hole numbers actually entered.</summary>
    public readonly record struct RoundShape(bool FullRound, bool FrontNine, bool BackNine);

    /// <summary>
    /// Splits the entered holes on the turn and sums each side. Only the holes supplied count, so
    /// this is correct for a partially entered round as well as a finished one.
    /// </summary>
    public static RoundTotals ComputeTotals(IEnumerable<HoleScoreLine> holes)
    {
        int scoreOut = 0, scoreIn = 0, parEntered = 0, holeCount = 0;

        foreach (var hole in holes)
        {
            if (hole.HoleNumber <= 9) scoreOut += hole.Score;
            else scoreIn += hole.Score;

            parEntered += hole.Par;
            holeCount++;
        }

        return new RoundTotals(scoreOut, scoreIn, scoreOut + scoreIn, parEntered, holeCount);
    }

    /// <summary>
    /// Overwrites <paramref name="target"/>'s scoring distribution from the entered holes.
    /// Counters are assigned, not incremented, so calling this repeatedly on the same row is safe.
    /// </summary>
    public static void ApplyScoringDistribution(RoundStat target, IEnumerable<HoleScoreLine> holes)
    {
        int holeInOne = 0, doubleEagles = 0, eagles = 0, birdies = 0, pars = 0, bogies = 0, doubleBogies = 0, tripleOrWorse = 0;

        foreach (var hole in holes)
        {
            switch (hole.Score - hole.Par)
            {
                case <= -3:
                    if (hole.Score == 1) holeInOne++;
                    else doubleEagles++;
                    break;
                case -2: eagles++; break;
                case -1: birdies++; break;
                case 0: pars++; break;
                case 1: bogies++; break;
                case 2: doubleBogies++; break;
                default: tripleOrWorse++; break;
            }
        }

        target.HoleInOne = holeInOne;
        target.DoubleEagles = doubleEagles;
        target.Eagles = eagles;
        target.Birdies = birdies;
        target.Pars = pars;
        target.Bogies = bogies;
        target.DoubleBogies = doubleBogies;
        target.TripleOrWorse = tripleOrWorse;
    }

    /// <summary>
    /// Derives the round's shape flags from the holes entered. A finished eighteen sets all three,
    /// matching how the TGTR importer reads them (finished front / finished back / finished round).
    /// </summary>
    public static RoundShape ClassifyShape(IReadOnlyCollection<int> holeNumbers, bool teeboxIsNineHole)
    {
        var played = holeNumbers as HashSet<int> ?? [.. holeNumbers];

        var front = Enumerable.Range(1, 9).All(played.Contains);
        if (teeboxIsNineHole)
        {
            // A nine-hole teebox only has holes 1-9, so covering them is a full round.
            return new RoundShape(front, front, false);
        }

        var back = Enumerable.Range(10, 9).All(played.Contains);
        return new RoundShape(front && back, front, back);
    }

    /// <summary>
    /// Hole numbers still needed before the round forms a complete eighteen, front nine, or back
    /// nine. Empty means the round is ready to post. The target is inferred from what is already
    /// entered: holes on both sides of the turn aim at a full eighteen, otherwise at that nine.
    /// </summary>
    public static IReadOnlyList<int> MissingHoles(IReadOnlyCollection<int> holeNumbers, bool teeboxIsNineHole)
    {
        var played = holeNumbers as HashSet<int> ?? [.. holeNumbers];
        if (played.Count == 0) return [];

        var hasFront = played.Any(n => n <= 9);
        var hasBack = played.Any(n => n > 9);

        IEnumerable<int> target = (teeboxIsNineHole, hasFront, hasBack) switch
        {
            (true, _, _) => Enumerable.Range(1, 9),
            (_, true, true) => Enumerable.Range(1, 18),
            (_, _, true) => Enumerable.Range(10, 9),
            _ => Enumerable.Range(1, 9)
        };

        return [.. target.Where(n => !played.Contains(n))];
    }

    /// <summary>
    /// Builds the shot rows for one hole, numbering them 1..n by list position. The client's
    /// <see cref="ShotData.ShotNumber"/> is deliberately ignored — the server owns the ordering.
    /// </summary>
    public static List<Shot> BuildShots(long scoreId, IReadOnlyList<ShotData> shots, string userId, DateOnly today)
    {
        var built = new List<Shot>(shots.Count);
        var shotNumber = 1;

        foreach (var shot in shots)
        {
            built.Add(new Shot
            {
                ScoreId = scoreId,
                ShotNumber = shotNumber++,
                StartDistance = shot.StartDistance,
                StartDistanceUnit = shot.StartDistanceUnit,
                StartLie = (int)shot.StartLie,
                EndDistance = shot.EndDistance,
                EndDistanceUnit = shot.EndDistanceUnit,
                EndLie = shot.EndLie.HasValue ? (int)shot.EndLie.Value : null,
                PenaltyStrokes = shot.PenaltyStrokes,
                CreatedBy = userId,
                CreatedOn = today,
                UpdatedBy = userId,
                UpdatedOn = today,
                IsDeleted = false
            });
        }

        return built;
    }

    /// <summary>
    /// Copies the stat fields the client is trusted with straight onto <paramref name="target"/>.
    /// Used for hole-stats-only rounds, where there are no shots to derive anything from.
    /// </summary>
    public static void ApplyClientHoleStat(HoleStat target, IHoleStatSource source)
    {
        target.HitFairway = source.HitFairway;
        target.MissFairwayType = source.MissFairwayType;
        target.HitGreen = source.HitGreen;
        target.MissGreenType = source.MissGreenType;
        target.NumberOfPutts = source.NumberOfPutts;
        target.ApproachYardage = source.ApproachYardage;
        target.TeeShotOutOfPosition = source.TeeShotOutOfPosition;
        target.ApproachShotOutOfPosition = source.ApproachShotOutOfPosition;
        target.TeeShotPenalty = source.TeeShotPenalty;
        target.ApproachShotPenalty = source.ApproachShotPenalty;
    }

    /// <summary>
    /// Derives the stat fields a shot chain can prove (putts, fairway, green, approach yardage,
    /// penalties) and takes only the four it cannot — the miss directions and out-of-position
    /// flags — from the client.
    /// </summary>
    public static void ApplyDerivedHoleStat(HoleStat target, List<ShotData> shots, int par, IHoleStatSource source)
    {
        var (numberOfPutts, hitFairway, hitGreen, approachYardage, teeShotPenalty, approachShotPenalty) =
            StrokesGainedCalculator.DeriveHoleStatFromShots(shots, par);

        target.HitFairway = hitFairway;
        target.HitGreen = hitGreen;
        target.NumberOfPutts = numberOfPutts;
        target.ApproachYardage = approachYardage;
        target.TeeShotPenalty = teeShotPenalty;
        target.ApproachShotPenalty = approachShotPenalty;

        // Fields shots can't derive — accept them from the client if supplied
        target.MissFairwayType = source.MissFairwayType;
        target.MissGreenType = source.MissGreenType;
        target.TeeShotOutOfPosition = source.TeeShotOutOfPosition;
        target.ApproachShotOutOfPosition = source.ApproachShotOutOfPosition;
    }
}

/// <summary>
/// The per-hole stat fields a client can send, shared by the atomic path's
/// <c>HoleScoreEntry</c> and the incremental path's <c>UpsertHoleRequest</c> so one set of
/// helpers serves both request shapes.
/// </summary>
public interface IHoleStatSource
{
    bool? HitFairway { get; }
    long? MissFairwayType { get; }
    bool? HitGreen { get; }
    long? MissGreenType { get; }
    short? NumberOfPutts { get; }
    int? ApproachYardage { get; }
    bool TeeShotOutOfPosition { get; }
    bool ApproachShotOutOfPosition { get; }
    bool TeeShotPenalty { get; }
    bool ApproachShotPenalty { get; }
}
