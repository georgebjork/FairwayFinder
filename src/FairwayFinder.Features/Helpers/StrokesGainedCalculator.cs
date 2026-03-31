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
        int penaltyStrokes,
        BaselineLevel level = BaselineLevel.Scratch)
    {
        var expectedStart = StrokesGainedBaseline.GetExpectedStrokes(startDistance, startUnit, startLie, level);

        var expectedEnd = (endDistance == null || endLie == null)
            ? 0.0 // holed out
            : StrokesGainedBaseline.GetExpectedStrokes(endDistance.Value, endUnit!, endLie.Value, level);

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
                shot.PenaltyStrokes, level);

            var category = ClassifyShot(shot.StartLie, shot.StartDistance, holePar);

            shot.StrokesGained = Math.Round(sg, 2);
            shot.Category = category;

            result.SgTotal += sg;

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

        // Round to 2 decimal places
        result.SgTotal = Math.Round(result.SgTotal, 2);
        result.SgPutting = Math.Round(result.SgPutting, 2);
        result.SgOffTheTee = Math.Round(result.SgOffTheTee, 2);
        result.SgApproach = Math.Round(result.SgApproach, 2);
        result.SgAroundTheGreen = Math.Round(result.SgAroundTheGreen, 2);

        return result;
    }

    /// <summary>
    /// Calculates the SG summary for an entire round.
    /// </summary>
    public static StrokesGainedSummary CalculateRoundSg(
        RoundResponse round, BaselineLevel level = BaselineLevel.Scratch)
    {
        var summary = new StrokesGainedSummary { RoundsIncluded = 1 };

        var holesWithShots = round.Holes.Where(h => h.Shots is { Count: > 0 }).ToList();
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
    public static (short? numberOfPutts, bool? hitFairway, bool? hitGreen, int? approachYardage, bool? teeShotOb, bool? approachShotOb)
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

        // Tee shot OB
        bool? teeShotOb = shots[0].PenaltyStrokes > 0;

        // Approach shot OB
        bool? approachShotOb = approachShot?.PenaltyStrokes > 0;

        return (numberOfPutts, hitFairway, hitGreen, approachYardage, teeShotOb, approachShotOb);
    }

    /// <summary>
    /// Validates shot data integrity for a hole.
    /// Returns a list of validation errors (empty = valid).
    /// </summary>
    public static List<string> ValidateShots(List<ShotData> shots, int holeYardage, int holeScore)
    {
        var errors = new List<string>();

        if (shots.Count == 0)
        {
            errors.Add("At least 1 shot is required per hole.");
            return errors;
        }

        // Shot 1 must start from tee at hole yardage
        if (shots[0].StartLie != LieType.Tee)
            errors.Add("First shot must start from the tee.");
        if (shots[0].StartDistance != holeYardage)
            errors.Add($"First shot start distance ({shots[0].StartDistance}) must equal hole yardage ({holeYardage}).");

        // Last shot must be holed
        var lastShot = shots[^1];
        if (lastShot.EndDistance != null || lastShot.EndLie != null)
            errors.Add("Last shot must be holed (EndDistance and EndLie must be null).");

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
        foreach (var shot in shots)
        {
            if (shot.PenaltyStrokes < 0 || shot.PenaltyStrokes > 2)
                errors.Add($"Shot {shot.ShotNumber}: penalty strokes must be 0, 1, or 2.");
        }

        return errors;
    }

    private static double? CalculateTrendSlope(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return null;

        var (slope, _) = StatsCalculator.CalculateLinearRegression(values);
        return Math.Round(slope, 3);
    }
}
