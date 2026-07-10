using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// All strokes gained calculation methods — per-shot, per-hole, per-round, and multi-round.
/// </summary>
public static class StrokesGainedCalculator
{
    /// <summary>
    /// Calculates strokes gained for a single shot.
    /// </summary>
    public static double CalculateShotSg(
        int startDistance, string startUnit, LieType startLie,
        int? endDistance, string? endUnit, LieType? endLie,
        int penaltyStrokes)
    {
        var expectedStart = StrokesGainedBaseline.GetExpectedStrokes(startDistance, startUnit, startLie);

        var expectedEnd = (endDistance == null || endLie == null)
            ? 0.0 // holed out
            : StrokesGainedBaseline.GetExpectedStrokes(endDistance.Value, endUnit!, endLie.Value);

        return expectedStart - expectedEnd - (1 + penaltyStrokes);
    }

    /// <summary>
    /// Classifies a shot into an SG category based on start position and hole par.
    /// </summary>
    public static ShotCategory ClassifyShot(LieType startLie, int startDistance, int holePar)
    {
        if (startLie == LieType.Green)
            return ShotCategory.Putting;
        if (startLie == LieType.Tee && holePar >= 4)
            return ShotCategory.OffTheTee;
        if (startLie == LieType.Tee && holePar == 3)
            return ShotCategory.Approach;
        if (startDistance <= 50)
            return ShotCategory.AroundTheGreen;
        return ShotCategory.Approach;
    }

    /// <summary>
    /// Calculates the SG breakdown for a single hole from its shots.
    /// </summary>
    public static StrokesGainedHoleResult CalculateHoleSg(
        List<ShotData> shots, int holePar, int holeNumber, int holeScore,
        BaselineLevel level = BaselineLevel.Scratch)
    {
        var result = new StrokesGainedHoleResult
        {
            HoleNumber = holeNumber,
            Par = holePar,
            Score = holeScore,
            Shots = shots
        };

        foreach (var shot in shots)
        {
            var sg = CalculateShotSg(
                shot.StartDistance, shot.StartDistanceUnit, shot.StartLie,
                shot.EndDistance, shot.EndDistanceUnit, shot.EndLie,
                shot.PenaltyStrokes);

            var category = ClassifyShot(shot.StartLie, shot.StartDistance, holePar);

            shot.StrokesGained = Math.Round(sg, 2);
            shot.Category = category;

            switch (category)
            {
                case ShotCategory.Putting:
                    result.SgPutting += sg;
                    break;
                case ShotCategory.OffTheTee:
                    result.SgOffTheTee += sg;
                    break;
                case ShotCategory.Approach:
                    result.SgApproach += sg;
                    break;
                case ShotCategory.AroundTheGreen:
                    result.SgAroundTheGreen += sg;
                    break;
            }
        }

        // Apply the golfer-level offset, distributed per hole (offset values are 18-hole totals),
        // so per-hole SG, the round summary, and averages all read relative to the selected level.
        // Tour returns a zero offset (raw SG vs the benchmark).
        var offsets = StrokesGainedBaseline.GetRoundOffsets(level);
        const double perHole = 1.0 / StrokesGainedBaseline.OffsetRoundHoles;
        result.SgOffTheTee += offsets.OffTheTee * perHole;
        result.SgApproach += offsets.Approach * perHole;
        result.SgAroundTheGreen += offsets.AroundTheGreen * perHole;
        result.SgPutting += offsets.Putting * perHole;

        // Round categories, then derive the total from them so the parts always sum to the total.
        result.SgPutting = Math.Round(result.SgPutting, 2);
        result.SgOffTheTee = Math.Round(result.SgOffTheTee, 2);
        result.SgApproach = Math.Round(result.SgApproach, 2);
        result.SgAroundTheGreen = Math.Round(result.SgAroundTheGreen, 2);
        result.SgTotal = Math.Round(
            result.SgPutting + result.SgOffTheTee + result.SgApproach + result.SgAroundTheGreen, 2);

        return result;
    }

    /// <summary>
    /// Calculates the SG summary for an entire round.
    /// </summary>
    public static StrokesGainedSummary CalculateRoundSg(
        RoundResponse round, BaselineLevel level = BaselineLevel.Scratch)
    {
        var summary = new StrokesGainedSummary { RoundsIncluded = 1 };

        // Skip holes whose stored shots are malformed so corrupt legacy data doesn't fabricate SG.
        var holesWithShots = round.Holes
            .Where(h => h.Shots is { Count: > 0 } && h.Score.HasValue && AreShotsScorable(h.Shots, h.Score.Value))
            .ToList();
        summary.HolesWithShots = holesWithShots.Count;

        foreach (var hole in holesWithShots)
        {
            var holeSg = CalculateHoleSg(hole.Shots!, hole.Par, hole.HoleNumber, hole.Score ?? 0, level);
            summary.SgTotal += holeSg.SgTotal;
            summary.SgPutting += holeSg.SgPutting;
            summary.SgOffTheTee += holeSg.SgOffTheTee;
            summary.SgApproach += holeSg.SgApproach;
            summary.SgAroundTheGreen += holeSg.SgAroundTheGreen;
        }

        summary.SgTeeToGreen = summary.SgOffTheTee + summary.SgApproach + summary.SgAroundTheGreen;

        // Round values
        summary.SgTotal = Math.Round(summary.SgTotal, 2);
        summary.SgPutting = Math.Round(summary.SgPutting, 2);
        summary.SgTeeToGreen = Math.Round(summary.SgTeeToGreen, 2);
        summary.SgOffTheTee = Math.Round(summary.SgOffTheTee, 2);
        summary.SgApproach = Math.Round(summary.SgApproach, 2);
        summary.SgAroundTheGreen = Math.Round(summary.SgAroundTheGreen, 2);

        return summary;
    }

    /// <summary>
    /// Average SG stats across multiple rounds.
    /// </summary>
    public static StrokesGainedSummary CalculateAverageSg(
        List<RoundResponse> rounds, BaselineLevel level = BaselineLevel.Scratch)
    {
        if (rounds.Count == 0)
            return new StrokesGainedSummary();

        var roundSummaries = rounds
            .Select(r => CalculateRoundSg(r, level))
            .ToList();

        var count = roundSummaries.Count;
        var summary = new StrokesGainedSummary
        {
            RoundsIncluded = count,
            HolesWithShots = roundSummaries.Sum(s => s.HolesWithShots),
            SgTotal = Math.Round(roundSummaries.Average(s => s.SgTotal), 2),
            SgPutting = Math.Round(roundSummaries.Average(s => s.SgPutting), 2),
            SgTeeToGreen = Math.Round(roundSummaries.Average(s => s.SgTeeToGreen), 2),
            SgOffTheTee = Math.Round(roundSummaries.Average(s => s.SgOffTheTee), 2),
            SgApproach = Math.Round(roundSummaries.Average(s => s.SgApproach), 2),
            SgAroundTheGreen = Math.Round(roundSummaries.Average(s => s.SgAroundTheGreen), 2)
        };

        // Calculate trends if enough rounds (min 3)
        if (count >= 3)
        {
            var orderedSummaries = rounds
                .OrderBy(r => r.DatePlayed)
                .Select(r => CalculateRoundSg(r, level))
                .ToList();

            summary.SgTotalTrend = CalculateTrendSlope(orderedSummaries.Select(s => s.SgTotal).ToList());
            summary.SgPuttingTrend = CalculateTrendSlope(orderedSummaries.Select(s => s.SgPutting).ToList());
            summary.SgTeeToGreenTrend = CalculateTrendSlope(orderedSummaries.Select(s => s.SgTeeToGreen).ToList());
            summary.SgOffTheTeeTrend = CalculateTrendSlope(orderedSummaries.Select(s => s.SgOffTheTee).ToList());
            summary.SgApproachTrend = CalculateTrendSlope(orderedSummaries.Select(s => s.SgApproach).ToList());
            summary.SgAroundTheGreenTrend = CalculateTrendSlope(orderedSummaries.Select(s => s.SgAroundTheGreen).ToList());
        }

        return summary;
    }

    /// <summary>
    /// SG trend over time for a specific category (for charting).
    /// </summary>
    public static (List<StrokesGainedTrendPoint> Points, double? Slope) CalculateSgTrend(
        List<RoundResponse> rounds, ShotCategory? category, BaselineLevel level = BaselineLevel.Scratch)
    {
        var orderedRounds = rounds.OrderBy(r => r.DatePlayed).ToList();
        var points = new List<StrokesGainedTrendPoint>();

        foreach (var round in orderedRounds)
        {
            var roundSg = CalculateRoundSg(round, level);
            var value = category switch
            {
                ShotCategory.Putting => roundSg.SgPutting,
                ShotCategory.OffTheTee => roundSg.SgOffTheTee,
                ShotCategory.Approach => roundSg.SgApproach,
                ShotCategory.AroundTheGreen => roundSg.SgAroundTheGreen,
                null => roundSg.SgTotal, // null = SG:Total
                _ => roundSg.SgTotal
            };

            points.Add(new StrokesGainedTrendPoint
            {
                RoundId = round.RoundId,
                DatePlayed = round.DatePlayed,
                CourseName = round.CourseName,
                Value = Math.Round(value, 2)
            });
        }

        // Calculate moving average (3-round window)
        for (int i = 0; i < points.Count; i++)
        {
            if (i >= 2)
            {
                var avg = (points[i].Value + points[i - 1].Value + points[i - 2].Value) / 3.0;
                points[i].MovingAverage = Math.Round(avg, 2);
            }
        }

        double? slope = null;
        if (points.Count >= 3)
        {
            slope = CalculateTrendSlope(points.Select(p => p.Value).ToList());
        }

        return (points, slope);
    }

    /// <summary>
    /// Derives HoleStat fields from shot data.
    /// </summary>
    public static (short? numberOfPutts, bool? hitFairway, bool? hitGreen, int? approachYardage, bool? teeShotPenalty, bool? approachShotPenalty)
        DeriveHoleStatFromShots(List<ShotData> shots, int holePar)
    {
        if (shots.Count == 0)
            return (null, null, null, null, null, null);

        // Number of putts = shots starting on the green
        short numberOfPutts = (short)shots.Count(s => s.StartLie == LieType.Green);

        // Hit fairway (par 4/5 only) = first shot's end lie is fairway
        bool? hitFairway = null;
        if (holePar >= 4 && shots.Count > 0)
        {
            hitFairway = shots[0].EndLie == LieType.Fairway;
        }

        // Hit green in regulation: the shot that reaches the green must be
        // shot number <= (par - 2), accounting for penalties
        bool? hitGreen = null;
        var firstPuttIndex = shots.FindIndex(s => s.StartLie == LieType.Green);
        if (firstPuttIndex > 0)
        {
            // The shot before the first putt landed on the green
            var shotThatReachedGreen = shots[firstPuttIndex - 1];
            var strokesUsed = 0;
            for (int i = 0; i < firstPuttIndex; i++)
            {
                strokesUsed += 1 + shots[i].PenaltyStrokes;
            }
            hitGreen = strokesUsed <= (holePar - 2);
        }
        else if (firstPuttIndex == 0)
        {
            // Started on green (shouldn't happen from tee, but handle it)
            hitGreen = holePar >= 2;
        }
        else
        {
            // No putts (chipped in) — check if holed from off-green in regulation
            var lastShot = shots[^1];
            if (lastShot.EndDistance == null) // holed out
            {
                var strokesUsed = 0;
                for (int i = 0; i < shots.Count; i++)
                {
                    strokesUsed += 1 + shots[i].PenaltyStrokes;
                }
                hitGreen = strokesUsed <= (holePar - 2);
            }
        }

        // Approach yardage = StartDistance of the shot classified as Approach
        int? approachYardage = null;
        var approachShot = shots.FirstOrDefault(s =>
        {
            var cat = ClassifyShot(s.StartLie, s.StartDistance, holePar);
            return cat == ShotCategory.Approach;
        });
        if (approachShot != null)
        {
            approachYardage = approachShot.StartDistance;
        }

        // Tee shot penalty
        bool? teeShotPenalty = shots[0].PenaltyStrokes > 0;

        // Approach shot penalty
        bool? approachShotPenalty = approachShot?.PenaltyStrokes > 0;

        return (numberOfPutts, hitFairway, hitGreen, approachYardage, teeShotPenalty, approachShotPenalty);
    }

    /// <summary>
    /// Validates shot data integrity for a hole. Returns a list of validation errors (empty = valid).
    /// Shot numbering in messages is 1-based by list position (the server re-numbers shots on save,
    /// so the client-supplied <see cref="ShotData.ShotNumber"/> is not relied upon here).
    /// <paramref name="holeYardage"/> is optional: when null, the first-shot-start-distance check is
    /// skipped (callers that don't have the teebox yardage handy, e.g. the API validators).
    /// </summary>
    public static List<string> ValidateShots(List<ShotData> shots, int holeScore, int? holeYardage = null)
    {
        var errors = new List<string>();

        if (shots.Count == 0)
        {
            errors.Add("At least 1 shot is required per hole.");
            return errors;
        }

        // Shot 1 must start from the tee (at hole yardage when known).
        if (shots[0].StartLie != LieType.Tee)
            errors.Add("First shot must start from the tee.");
        if (holeYardage.HasValue && shots[0].StartDistance != holeYardage.Value)
            errors.Add($"First shot start distance ({shots[0].StartDistance}) must equal hole yardage ({holeYardage.Value}).");

        // Every shot must start from a real position (distance-to-hole >= 1). A 0/negative distance
        // is read as "in the hole" by the SG math (GetExpectedStrokes returns 0), which corrupts the
        // result — this is the guard that catches placeholder rows like StartDistance = 0.
        for (int i = 0; i < shots.Count; i++)
        {
            if (shots[i].StartDistance < 1)
                errors.Add($"Shot {i + 1}: start distance ({shots[i].StartDistance}) must be at least 1.");
        }

        // Only the final shot may be holed. Holed is encoded as EndDistance/EndLie == null — never 0.
        // Every non-final shot must end at a real position (distance >= 1).
        for (int i = 0; i < shots.Count; i++)
        {
            var shot = shots[i];
            var isHoled = shot.EndDistance == null && shot.EndLie == null;

            if (i == shots.Count - 1)
            {
                if (!isHoled)
                    errors.Add("Last shot must be holed (EndDistance and EndLie must be null).");
            }
            else if (shot.EndDistance == null || shot.EndLie == null)
            {
                errors.Add($"Shot {i + 1}: only the final shot may be holed (EndDistance and EndLie must be set).");
            }
            else if (shot.EndDistance < 1)
            {
                errors.Add($"Shot {i + 1}: end distance ({shot.EndDistance}) must be at least 1.");
            }
        }

        // Chain continuity
        for (int i = 0; i < shots.Count - 1; i++)
        {
            var current = shots[i];
            var next = shots[i + 1];

            if (current.EndDistance != next.StartDistance)
                errors.Add($"Shot {i + 1} end distance ({current.EndDistance}) must equal shot {i + 2} start distance ({next.StartDistance}).");
            if (current.EndLie != next.StartLie)
                errors.Add($"Shot {i + 1} end lie ({current.EndLie}) must equal shot {i + 2} start lie ({next.StartLie}).");
        }

        // Score validation
        var calculatedScore = shots.Count + shots.Sum(s => s.PenaltyStrokes);
        if (calculatedScore != holeScore)
            errors.Add($"Score mismatch: shots ({shots.Count}) + penalties ({shots.Sum(s => s.PenaltyStrokes)}) = {calculatedScore}, expected {holeScore}.");

        // Penalty validation
        for (int i = 0; i < shots.Count; i++)
        {
            if (shots[i].PenaltyStrokes < 0 || shots[i].PenaltyStrokes > 2)
                errors.Add($"Shot {i + 1}: penalty strokes must be 0, 1, or 2.");
        }

        return errors;
    }

    /// <summary>
    /// True when a hole's shots form a valid, scorable chain (no validation errors). Used on read
    /// paths to skip strokes gained for holes whose stored shots are malformed (e.g. legacy rows with
    /// a StartDistance = 0 placeholder) rather than emitting fabricated SG.
    /// </summary>
    public static bool AreShotsScorable(List<ShotData> shots, int holeScore)
        => ValidateShots(shots, holeScore).Count == 0;

    private static double? CalculateTrendSlope(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return null;

        var (slope, _) = StatsCalculator.CalculateLinearRegression(values);
        return Math.Round(slope, 3);
    }

    /// <summary>
    /// Averages a collection of per-hole SG records into a single block.
    /// All sub-values are rounded to 2 dp, matching the precision used by
    /// <c>AggregateStoredSg</c> in <c>StatsService</c>. Caller is responsible
    /// for filtering out null/empty inputs — passing an empty list will throw
    /// (LINQ Average) so check the count first.
    /// </summary>
    public static HoleAverageSg AverageHoleSg(IReadOnlyList<StrokesGainedHoleResult> sgs)
    {
        return new HoleAverageSg
        {
            Count            = sgs.Count,
            SgTotal          = Math.Round(sgs.Average(s => s.SgTotal),          2),
            SgOffTheTee      = Math.Round(sgs.Average(s => s.SgOffTheTee),      2),
            SgApproach       = Math.Round(sgs.Average(s => s.SgApproach),       2),
            SgAroundTheGreen = Math.Round(sgs.Average(s => s.SgAroundTheGreen), 2),
            SgPutting        = Math.Round(sgs.Average(s => s.SgPutting),        2),
        };
    }
}
