using FairwayFinder.Data;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class PublicProfileService : IPublicProfileService
{
    /// <summary>How many 18-hole rounds the trend chart shows.</summary>
    public const int TrendSize = 20;

    /// <summary>How many rounds the recent-rounds table shows.</summary>
    public const int RecentRoundsSize = 5;

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IStatsService _statsService;
    private readonly IRoundService _roundService;

    public PublicProfileService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IStatsService statsService,
        IRoundService roundService)
    {
        _dbContextFactory = dbContextFactory;
        _statsService = statsService;
        _roundService = roundService;
    }

    public async Task<PublicProfileResponse?> GetBySlugAsync(string slug)
    {
        var normalized = PublicSlugRules.Normalize(slug);
        if (normalized.Length == 0)
            return null;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var owner = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.PublicSlug == normalized && p.IsPublic && !p.IsDeleted)
            .Join(dbContext.Users, p => p.UserId, u => u.Id, (p, u) => new
            {
                Profile = p,
                u.FirstName,
                u.LastName,
                u.Handle,
                u.SgBaselineLevel
            })
            .FirstOrDefaultAsync();

        if (owner is null)
            return null;

        var profile = owner.Profile;
        var userId = profile.UserId;

        var stats = await _statsService.GetUserStatsAsync(userId, level: (BaselineLevel)owner.SgBaselineLevel);


        // The display list includes ExcludeFromStats rounds (it badges them); a public page counts
        // what the stats count, so drop them here. Both sources already skip incomplete rounds.
        var rounds = (await _roundService.GetRoundsByUserIdAsync(userId))
            .Where(r => !r.ExcludeFromStats && r.IsComplete)
            .ToList();

        // Trimmed: some imported course names carry stray whitespace.
        string? Course(string name) => profile.ShowCourseNames ? name.Trim() : null;

        var now = DateTime.UtcNow;
        var level = (BaselineLevel)owner.SgBaselineLevel;
        var best = stats.Scoring.Best18HoleRound;
        var bestRound = best is null ? null : rounds.FirstOrDefault(r => r.RoundId == best.RoundId);
        var distribution = stats.ScoringDistribution;
        var sg = stats.StrokesGained.Summary;
        var recent = rounds.Take(RecentRoundsSize).ToList();

        // Scorecards for every round the page links to. They ride on the recent-rounds toggle: with
        // it off there are no round pages at all, and the best round isn't linked either.
        var keys = ScorecardKeys(rounds);
        var scorecards = new List<PublicScorecard>();
        if (profile.ShowRecentRounds)
        {
            var linked = recent.Select(r => r.RoundId)
                .Concat(bestRound is null ? [] : [bestRound.RoundId])
                .Distinct();

            foreach (var roundId in linked)
            {
                var detail = await _roundService.GetRoundByIdAsync(roundId, level);
                if (detail is { IsComplete: true, ExcludeFromStats: false })
                    scorecards.Add(ToScorecard(detail, keys[roundId], profile.ShowCourseNames, profile.ShowStrokesGained));
            }
        }

        var published = scorecards.Select(c => c.Key).ToHashSet();
        string? KeyFor(long roundId) => keys.TryGetValue(roundId, out var key) && published.Contains(key) ? key : null;

        return new PublicProfileResponse
        {
            // Never DisplayNameHelper.Build(user): it falls back to UserName, which is the email.
            DisplayName = DisplayNameHelper.Build(owner.FirstName, owner.LastName, owner.Handle),
            Slug = normalized,
            GeneratedOn = now,
            Headline = new PublicProfileHeadline
            {
                HandicapIndex = HandicapCalculator.Calculate(rounds
                    .Where(r => r.FullRound && !r.Teebox.IsNineHole)
                    .Select(r => new HandicapCalculator.Round(r.DatePlayed, r.Score, r.Teebox.Rating, r.Teebox.Slope))),
                ScoringAverage18 = stats.Scoring.Average18HoleScore is { } avg ? Math.Round(avg, 1) : null,
                RoundsThisYear = rounds.Count(r => r.DatePlayed.Year == now.Year),
                TotalRounds = stats.TotalRounds,
                BestRound = best is null ? null : new PublicBestRound
                {
                    Key = KeyFor(best.RoundId),
                    Score = best.Score,
                    ToPar = bestRound?.ScoreToPar ?? 0,
                    DatePlayed = best.DatePlayed,
                    CourseName = Course(best.CourseName)
                }
            },
            StrokesGained = !profile.ShowStrokesGained || sg is null ? null : new PublicStrokesGained
            {
                OffTheTee = Math.Round(sg.SgOffTheTee, 2),
                Approach = Math.Round(sg.SgApproach, 2),
                AroundTheGreen = Math.Round(sg.SgAroundTheGreen, 2),
                Putting = Math.Round(sg.SgPutting, 2),
                Total = Math.Round(sg.SgTotal, 2),
                RoundsIncluded = sg.RoundsIncluded
            },
            ScoringDistribution = new PublicScoringDistribution
            {
                EaglesOrBetter = distribution.HolesInOne + distribution.DoubleEagles + distribution.Eagles,
                Birdies = distribution.Birdies,
                Pars = distribution.Pars,
                Bogeys = distribution.Bogeys,
                DoubleBogeys = distribution.DoubleBogeys,
                TripleOrWorse = distribution.TripleOrWorse,
                TotalHoles = distribution.TotalHoles
            },
            Trend = stats.Scoring.ScoreTrend18Hole
                .TakeLast(TrendSize)
                .Select(p => new PublicTrendPoint
                {
                    DatePlayed = p.DatePlayed,
                    Score = p.Score,
                    CourseName = Course(p.CourseName)
                })
                .ToList(),
            RecentRounds = !profile.ShowRecentRounds ? null : recent
                .Select(r => new PublicRecentRound
                {
                    Key = KeyFor(r.RoundId),
                    DatePlayed = r.DatePlayed,
                    Score = r.Score,
                    ToPar = r.ScoreToPar,
                    FullRound = r.FullRound,
                    CourseName = Course(r.CourseName)
                })
                .ToList(),
            Scorecards = scorecards
        };
    }

    /// <summary>
    /// A stable URL key per round: the date played, then <c>-2</c>, <c>-3</c>… for later rounds that
    /// day in the order they were created. Computed over every counted round, not just the published
    /// ones, so a round's key doesn't change as the recent-rounds window moves past it.
    /// </summary>
    public static Dictionary<long, string> ScorecardKeys(IEnumerable<RoundResponse> rounds) =>
        rounds
            .GroupBy(r => r.DatePlayed)
            .SelectMany(day => day
                .OrderBy(r => r.RoundId)
                .Select((r, i) => (r.RoundId, Key: i == 0
                    ? day.Key.ToString("yyyy-MM-dd")
                    : $"{day.Key:yyyy-MM-dd}-{i + 1}")))
            .ToDictionary(x => x.RoundId, x => x.Key);

    private static PublicScorecard ToScorecard(RoundResponse round, string key, bool showCourseNames, bool showStrokesGained)
    {
        // Only the holes actually played: a nine on an 18-hole teebox carries the other nine unscored.
        var holes = round.Holes
            .Where(h => h.Score.HasValue)
            .OrderBy(h => h.HoleNumber)
            .ToList();

        var tracked = round.UsingHoleStats && holes.Any(h => h.Stats is not null);
        var fairwayHoles = holes.Where(h => h.Par > 3 && h.Stats?.HitFairway is not null).ToList();
        var greenHoles = holes.Where(h => h.Stats?.HitGreen is not null).ToList();
        var puttHoles = holes.Where(h => h.Stats?.NumberOfPutts is not null).ToList();
        var sg = round.StrokesGained;

        return new PublicScorecard
        {
            Key = key,
            DatePlayed = round.DatePlayed,
            CourseName = showCourseNames ? round.CourseName.Trim() : null,
            TeeboxName = round.Teebox.TeeboxName,
            Score = round.Score,
            ToPar = holes.Sum(h => h.Score!.Value) - holes.Sum(h => h.Par),
            Par = holes.Sum(h => h.Par),
            FullRound = round.FullRound,
            Holes = holes.Select(h => new PublicScorecardHole
            {
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Yardage = h.Yardage,
                Score = h.Score!.Value,
                Putts = h.Stats?.NumberOfPutts,
                Fairway = h.Par > 3 ? h.Stats?.HitFairway : null,
                Green = h.Stats?.HitGreen
            }).ToList(),
            FairwaysHit = tracked && fairwayHoles.Count > 0 ? fairwayHoles.Count(h => h.Stats!.HitFairway == true) : null,
            FairwaysTotal = tracked && fairwayHoles.Count > 0 ? fairwayHoles.Count : null,
            GreensHit = tracked && greenHoles.Count > 0 ? greenHoles.Count(h => h.Stats!.HitGreen == true) : null,
            GreensTotal = tracked && greenHoles.Count > 0 ? greenHoles.Count : null,
            Putts = tracked && puttHoles.Count > 0 ? puttHoles.Sum(h => h.Stats!.NumberOfPutts!.Value) : null,
            StrokesGained = !showStrokesGained || !round.UsingShotTracking || sg is null ? null : new PublicStrokesGained
            {
                OffTheTee = Math.Round(sg.SgOffTheTee, 2),
                Approach = Math.Round(sg.SgApproach, 2),
                AroundTheGreen = Math.Round(sg.SgAroundTheGreen, 2),
                Putting = Math.Round(sg.SgPutting, 2),
                Total = Math.Round(sg.SgTotal, 2),
                RoundsIncluded = 1
            }
        };
    }
}
