using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// Pure static functions for calculating stats from round data.
/// All methods are testable without database dependencies.
/// </summary>
public static class StatsCalculator
{

    /// <summary>
    /// Calculates the average score across all rounds
    /// </summary>
    public static double? CalculateAverageScore(IReadOnlyList<RoundResponse> rounds)
    {
        if (rounds.Count == 0) return null;
        return Math.Round(rounds.Average(r => r.Score), 1);
    }

    /// <summary>
    /// Calculates the score trend (last 5 rounds avg - previous 5 rounds avg)
    /// Negative = improvement, Positive = regression
    /// </summary>
    public static double? CalculateScoreTrend(IReadOnlyList<RoundResponse> rounds, int windowSize = 5)
    {
        if (rounds.Count < windowSize * 2) return null;

        var last5Avg = rounds.Take(windowSize).Average(r => r.Score);
        var previous5Avg = rounds.Skip(windowSize).Take(windowSize).Average(r => r.Score);
        
        return Math.Round(last5Avg - previous5Avg, 1);
    }

    /// <summary>
    /// Finds the best (lowest score) round
    /// </summary>
    public static BestRound? FindBestRound(IReadOnlyList<RoundResponse> rounds)
    {
        if (rounds.Count == 0) return null;

        var best = rounds.OrderBy(r => r.Score).First();
        return new BestRound
        {
            RoundId = best.RoundId,
            Score = best.Score,
            CourseName = best.CourseName,
            DatePlayed = best.DatePlayed
        };
    }

    /// <summary>
    /// Builds the score trend data for charting (oldest to newest)
    /// </summary>
    public static List<ScoreTrendPoint> BuildScoreTrend(IReadOnlyList<RoundResponse> rounds, int count)
    {
        return rounds
            .Take(count)
            .Reverse() // Oldest first for chart display
            .Select(r => new ScoreTrendPoint
            {
                RoundId = r.RoundId,
                DatePlayed = r.DatePlayed,
                Score = r.Score,
                CourseName = r.CourseName
            })
            .ToList();
    }

    /// <summary>
    /// Calculates stats per course (most played courses)
    /// </summary>
    public static List<CourseStats> CalculateCourseStats(IReadOnlyList<RoundResponse> rounds, int count)
    {
        return rounds
            .GroupBy(r => new { r.CourseId, r.CourseName })
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => new CourseStats
            {
                CourseId = g.Key.CourseId,
                CourseName = g.Key.CourseName,
                RoundCount = g.Count(),
                AverageScore = Math.Round(g.Average(r => (double)r.Score), 1)
            })
            .ToList();
    }

    /// <summary>
    /// Aggregates scoring distribution across all rounds
    /// </summary>
    public static ScoringDistribution AggregateScoringDistribution(IReadOnlyList<RoundResponse> rounds)
    {
        var roundsWithStats = rounds.Where(r => r.Stats != null).ToList();
        
        if (roundsWithStats.Count == 0)
        {
            return new ScoringDistribution();
        }

        return new ScoringDistribution
        {
            HolesInOne = roundsWithStats.Sum(r => r.Stats!.HolesInOne),
            DoubleEagles = roundsWithStats.Sum(r => r.Stats!.DoubleEagles),
            Eagles = roundsWithStats.Sum(r => r.Stats!.Eagles),
            Birdies = roundsWithStats.Sum(r => r.Stats!.Birdies),
            Pars = roundsWithStats.Sum(r => r.Stats!.Pars),
            Bogeys = roundsWithStats.Sum(r => r.Stats!.Bogeys),
            DoubleBogeys = roundsWithStats.Sum(r => r.Stats!.DoubleBogeys),
            TripleOrWorse = roundsWithStats.Sum(r => r.Stats!.TripleOrWorse),
            RoundCount = roundsWithStats.Count
        };
    }

    /// <summary>
    /// Calculates average scoring by par type (3, 4, 5)
    /// </summary>
    public static ParTypeScoring CalculateParTypeScoring(IReadOnlyList<RoundResponse> rounds)
    {
        var allHoles = rounds
            .SelectMany(r => r.Holes)
            .Where(h => h.Score.HasValue)
            .ToList();

        var par3Holes = allHoles.Where(h => h.Par == 3).ToList();
        var par4Holes = allHoles.Where(h => h.Par == 4).ToList();
        var par5Holes = allHoles.Where(h => h.Par == 5).ToList();

        return new ParTypeScoring
        {
            Par3Average = par3Holes.Count > 0 
                ? Math.Round(par3Holes.Average(h => (double)h.Score!.Value), 2) 
                : null,
            Par3Count = par3Holes.Count,
            Par4Average = par4Holes.Count > 0 
                ? Math.Round(par4Holes.Average(h => (double)h.Score!.Value), 2) 
                : null,
            Par4Count = par4Holes.Count,
            Par5Average = par5Holes.Count > 0 
                ? Math.Round(par5Holes.Average(h => (double)h.Score!.Value), 2) 
                : null,
            Par5Count = par5Holes.Count
        };
    }

    /// <summary>
    /// Calculates advanced stats (FIR%, GIR%, average putts) with trends
    /// </summary>
    public static AdvancedStats CalculateAdvancedStats(IReadOnlyList<RoundResponse> rounds)
    {
        var roundsWithHoleStats = rounds
            .Where(r => r.UsingHoleStats)
            .ToList();

        var result = new AdvancedStats
        {
            RoundsWithStats = roundsWithHoleStats.Count
        };

        if (roundsWithHoleStats.Count == 0)
        {
            return result;
        }

        var allHoleStats = roundsWithHoleStats
            .SelectMany(r => r.Holes.Select(h => new { r.RoundId, Hole = h }))
            .Where(x => x.Hole.Stats != null)
            .ToList();

        // FIR % (only par 4/5 holes)
        var fairwayHoles = allHoleStats
            .Where(x => x.Hole.Par > 3 && x.Hole.Stats!.HitFairway.HasValue)
            .ToList();
        
        if (fairwayHoles.Count > 0)
        {
            var fairwaysHit = fairwayHoles.Count(x => x.Hole.Stats!.HitFairway == true);
            result.FirPercent = Math.Round((double)fairwaysHit / fairwayHoles.Count * 100, 1);
        }

        // GIR % (all holes)
        var greenHoles = allHoleStats
            .Where(x => x.Hole.Stats!.HitGreen.HasValue)
            .ToList();
        
        if (greenHoles.Count > 0)
        {
            var greensHit = greenHoles.Count(x => x.Hole.Stats!.HitGreen == true);
            result.GirPercent = Math.Round((double)greensHit / greenHoles.Count * 100, 1);
        }

        // Average putts per round
        var holesWithPutts = allHoleStats
            .Where(x => x.Hole.Stats!.NumberOfPutts.HasValue)
            .ToList();
        
        if (holesWithPutts.Count > 0)
        {
            var puttsByRound = holesWithPutts
                .GroupBy(x => x.RoundId)
                .Select(g => g.Sum(x => x.Hole.Stats!.NumberOfPutts!.Value))
                .ToList();

            if (puttsByRound.Count > 0)
            {
                result.AveragePutts = Math.Round(puttsByRound.Average(), 1);
            }
        }

        // Calculate trends if we have at least 10 rounds with stats
        if (roundsWithHoleStats.Count >= 10)
        {
            CalculateAdvancedStatsTrends(result, roundsWithHoleStats);
        }

        return result;
    }

    private static void CalculateAdvancedStatsTrends(
        AdvancedStats result,
        List<RoundResponse> roundsWithHoleStats)
    {
        var recent5RoundIds = roundsWithHoleStats.Take(5).Select(r => r.RoundId).ToHashSet();
        var previous5RoundIds = roundsWithHoleStats.Skip(5).Take(5).Select(r => r.RoundId).ToHashSet();

        var holeStatsForTrends = roundsWithHoleStats
            .SelectMany(r => r.Holes.Select(h => new { r.RoundId, Hole = h }))
            .Where(x => x.Hole.Stats != null)
            .ToList();

        // FIR Trend
        var recent5FairwayHoles = holeStatsForTrends
            .Where(x => recent5RoundIds.Contains(x.RoundId) && x.Hole.Par > 3 && x.Hole.Stats!.HitFairway.HasValue)
            .ToList();
        var previous5FairwayHoles = holeStatsForTrends
            .Where(x => previous5RoundIds.Contains(x.RoundId) && x.Hole.Par > 3 && x.Hole.Stats!.HitFairway.HasValue)
            .ToList();

        if (recent5FairwayHoles.Count > 0 && previous5FairwayHoles.Count > 0)
        {
            var recent5Fir = (double)recent5FairwayHoles.Count(x => x.Hole.Stats!.HitFairway == true) / recent5FairwayHoles.Count * 100;
            var previous5Fir = (double)previous5FairwayHoles.Count(x => x.Hole.Stats!.HitFairway == true) / previous5FairwayHoles.Count * 100;
            result.FirPercentTrend = Math.Round(recent5Fir - previous5Fir, 1);
        }

        // GIR Trend
        var recent5GreenHoles = holeStatsForTrends
            .Where(x => recent5RoundIds.Contains(x.RoundId) && x.Hole.Stats!.HitGreen.HasValue)
            .ToList();
        var previous5GreenHoles = holeStatsForTrends
            .Where(x => previous5RoundIds.Contains(x.RoundId) && x.Hole.Stats!.HitGreen.HasValue)
            .ToList();

        if (recent5GreenHoles.Count > 0 && previous5GreenHoles.Count > 0)
        {
            var recent5Gir = (double)recent5GreenHoles.Count(x => x.Hole.Stats!.HitGreen == true) / recent5GreenHoles.Count * 100;
            var previous5Gir = (double)previous5GreenHoles.Count(x => x.Hole.Stats!.HitGreen == true) / previous5GreenHoles.Count * 100;
            result.GirPercentTrend = Math.Round(recent5Gir - previous5Gir, 1);
        }

        // Putts Trend
        var recent5PuttHoles = holeStatsForTrends
            .Where(x => recent5RoundIds.Contains(x.RoundId) && x.Hole.Stats!.NumberOfPutts.HasValue)
            .ToList();
        var previous5PuttHoles = holeStatsForTrends
            .Where(x => previous5RoundIds.Contains(x.RoundId) && x.Hole.Stats!.NumberOfPutts.HasValue)
            .ToList();

        if (recent5PuttHoles.Count > 0 && previous5PuttHoles.Count > 0)
        {
            var recent5PuttsByRound = recent5PuttHoles
                .GroupBy(x => x.RoundId)
                .Select(g => g.Sum(x => x.Hole.Stats!.NumberOfPutts!.Value))
                .ToList();
            var previous5PuttsByRound = previous5PuttHoles
                .GroupBy(x => x.RoundId)
                .Select(g => g.Sum(x => x.Hole.Stats!.NumberOfPutts!.Value))
                .ToList();

            if (recent5PuttsByRound.Count > 0 && previous5PuttsByRound.Count > 0)
            {
                var recent5AvgPutts = recent5PuttsByRound.Average();
                var previous5AvgPutts = previous5PuttsByRound.Average();
                result.AveragePuttsTrend = Math.Round(recent5AvgPutts - previous5AvgPutts, 1);
            }
        }
    }
}
