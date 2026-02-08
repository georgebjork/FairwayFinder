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
    public static double? CalculateAverageScore(IReadOnlyList<RoundResponse> rounds, bool fullRound = true)
    {
        if (rounds.Count == 0) return null;
        return Math.Round(rounds.Where(x => x.FullRound == fullRound).Average(r => r.Score), 1);
    }

    /// <summary>
    /// Calculates the score trend (last 5 rounds avg - previous 5 rounds avg)
    /// Negative = improvement, Positive = regression
    /// </summary>
    public static double? CalculateScoreTrend(IReadOnlyList<RoundResponse> rounds, int windowSize = 5, bool fullRound = true)
    {
        if (rounds.Count < windowSize * 2) return null;

        var filteredRounds = rounds.Where(x => x.FullRound == fullRound).ToList();
        
        if (filteredRounds.Count < windowSize * 2) return null;
        
        var last5Avg = filteredRounds.Take(windowSize).Average(r => r.Score);
        var previous5Avg = filteredRounds.Skip(windowSize).Take(windowSize).Average(r => r.Score);
        
        return Math.Round(last5Avg - previous5Avg, 1);
    }

    /// <summary>
    /// Finds the best (lowest score) round
    /// </summary>
    public static BestRound? FindBestRound(IReadOnlyList<RoundResponse> rounds, bool fullRound = true)
    {
        if (rounds.Count == 0) return null;

        var best = rounds.OrderBy(r => r.Score).First(x => x.FullRound == fullRound);
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
    public static List<ScoreTrendPoint> BuildScoreTrend(IReadOnlyList<RoundResponse> rounds, int count, bool fullRound = true)
    {
        var filteredRounds = rounds.Where(x => x.FullRound == fullRound);
        return filteredRounds
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
    /// Builds the putts trend data for charting (oldest to newest)
    /// Only includes rounds that have hole stats with putt data
    /// </summary>
    public static List<PuttsTrendPoint> BuildPuttsTrend(IReadOnlyList<RoundResponse> rounds, int count, bool fullRound = true)
    {
        var filteredRounds = rounds
            .Where(x => x.FullRound == fullRound && x.UsingHoleStats && x.TotalPutts > 0);
        
        return filteredRounds
            .Take(count)
            .Reverse() // Oldest first for chart display
            .Select(r => new PuttsTrendPoint
            {
                RoundId = r.RoundId,
                DatePlayed = r.DatePlayed,
                Putts = r.TotalPutts,
                CourseName = r.CourseName
            })
            .ToList();
    }

    /// <summary>
    /// Builds the FIR% trend data for charting (oldest to newest)
    /// Only includes rounds that have hole stats with fairway data (par 4/5 holes)
    /// </summary>
    public static List<FirTrendPoint> BuildFirTrend(IReadOnlyList<RoundResponse> rounds, int count)
    {
        var result = new List<FirTrendPoint>();
        
        var roundsWithStats = rounds
            .Where(x => x.UsingHoleStats)
            .Take(count)
            .ToList();
        
        foreach (var round in roundsWithStats)
        {
            var fairwayHoles = round.Holes
                .Where(h => h.Par > 3 && h.Stats?.HitFairway.HasValue == true)
                .ToList();
            
            if (fairwayHoles.Count > 0)
            {
                var fairwaysHit = fairwayHoles.Count(h => h.Stats!.HitFairway == true);
                result.Add(new FirTrendPoint
                {
                    RoundId = round.RoundId,
                    DatePlayed = round.DatePlayed,
                    FirPercent = Math.Round((double)fairwaysHit / fairwayHoles.Count * 100, 1),
                    FairwaysHit = fairwaysHit,
                    FairwayAttempts = fairwayHoles.Count,
                    CourseName = round.CourseName
                });
            }
        }
        
        // Reverse to get oldest first for chart display
        result.Reverse();
        return result;
    }

    /// <summary>
    /// Builds the GIR% trend data for charting (oldest to newest)
    /// Only includes rounds that have hole stats with green data
    /// </summary>
    public static List<GirTrendPoint> BuildGirTrend(IReadOnlyList<RoundResponse> rounds, int count)
    {
        var result = new List<GirTrendPoint>();
        
        var roundsWithStats = rounds
            .Where(x => x.UsingHoleStats)
            .Take(count)
            .ToList();
        
        foreach (var round in roundsWithStats)
        {
            var greenHoles = round.Holes
                .Where(h => h.Stats?.HitGreen.HasValue == true)
                .ToList();
            
            if (greenHoles.Count > 0)
            {
                var greensHit = greenHoles.Count(h => h.Stats!.HitGreen == true);
                result.Add(new GirTrendPoint
                {
                    RoundId = round.RoundId,
                    DatePlayed = round.DatePlayed,
                    GirPercent = Math.Round((double)greensHit / greenHoles.Count * 100, 1),
                    GreensHit = greensHit,
                    GreenAttempts = greenHoles.Count,
                    CourseName = round.CourseName
                });
            }
        }
        
        // Reverse to get oldest first for chart display
        result.Reverse();
        return result;
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

        var allHoleStats18Holes = roundsWithHoleStats.Where(x => x.FullRound)
            .SelectMany(r => r.Holes.Select(h => new { r.RoundId, Hole = h }))
            .Where(x => x.Hole.Stats != null)
            .ToList();
        
        var allHoleStats9Holes = roundsWithHoleStats.Where(x => !x.FullRound)
            .SelectMany(r => r.Holes.Select(h => new { r.RoundId, Hole = h }))
            .Where(x => x.Hole.Stats != null)
            .ToList();
        

        // FIR % (only par 4/5 holes)
        var fairwayHoles = allHoleStats18Holes
            .Where(x => x.Hole.Par > 3 && x.Hole.Stats!.HitFairway.HasValue)
            .ToList();
        
        if (fairwayHoles.Count > 0)
        {
            var fairwaysHit = fairwayHoles.Count(x => x.Hole.Stats!.HitFairway == true);
            result.FirPercent = Math.Round((double)fairwaysHit / fairwayHoles.Count * 100, 1);
        }

        // GIR % (all holes)
        var greenHoles = allHoleStats18Holes
            .Where(x => x.Hole.Stats!.HitGreen.HasValue)
            .ToList();
        
        if (greenHoles.Count > 0)
        {
            var greensHit = greenHoles.Count(x => x.Hole.Stats!.HitGreen == true);
            result.GirPercent = Math.Round((double)greensHit / greenHoles.Count * 100, 1);
        }

        // Average putts per round
        var holesWithPutts18Holes = allHoleStats18Holes
            .Where(x => x.Hole.Stats!.NumberOfPutts.HasValue)
            .ToList();
        
        if (holesWithPutts18Holes.Count > 0)
        {
            var puttsByRound18Holes = holesWithPutts18Holes
                .GroupBy(x => x.RoundId)
                .Select(g => g.Sum(x => x.Hole.Stats!.NumberOfPutts!.Value))
                .ToList();

            if (puttsByRound18Holes.Count > 0)
            {
                result.Average18HolePutts = Math.Round(puttsByRound18Holes.Average(), 1);
            }
        }
        
        var holesWithPutts9Holes = allHoleStats9Holes
            .Where(x => x.Hole.Stats!.NumberOfPutts.HasValue)
            .ToList();
        
        if (holesWithPutts9Holes.Count > 0)
        {
            var puttsByRound9Holes = holesWithPutts9Holes
                .GroupBy(x => x.RoundId)
                .Select(g => g.Sum(x => x.Hole.Stats!.NumberOfPutts!.Value))
                .ToList();

            if (puttsByRound9Holes.Count > 0)
            {
                result.Average9HolePutts = Math.Round(puttsByRound9Holes.Average(), 1);
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
        // All rounds (for FIR/GIR)
        var recent5Ids = roundsWithHoleStats.Take(5).Select(r => r.RoundId).ToHashSet();
        var previous5Ids = roundsWithHoleStats.Skip(5).Take(5).Select(r => r.RoundId).ToHashSet();

        // 18-hole rounds (for putting)
        var recent5FullIds = roundsWithHoleStats.Where(x => x.FullRound).Take(5).Select(r => r.RoundId).ToHashSet();
        var previous5FullIds = roundsWithHoleStats.Where(x => x.FullRound).Skip(5).Take(5).Select(r => r.RoundId).ToHashSet();

        // 9-hole rounds (for putting)
        var recent5NineIds = roundsWithHoleStats.Where(x => !x.FullRound).Take(5).Select(r => r.RoundId).ToHashSet();
        var previous5NineIds = roundsWithHoleStats.Where(x => !x.FullRound).Skip(5).Take(5).Select(r => r.RoundId).ToHashSet();

        var holeStatsForTrends = roundsWithHoleStats
            .SelectMany(r => r.Holes.Select(h => new { r.RoundId, Hole = h }))
            .Where(x => x.Hole.Stats != null)
            .ToList();

        // FIR Trend
        var recent5FairwayHoles = holeStatsForTrends
            .Where(x => recent5Ids.Contains(x.RoundId) && x.Hole.Par > 3 && x.Hole.Stats!.HitFairway.HasValue)
            .ToList();
        var previous5FairwayHoles = holeStatsForTrends
            .Where(x => previous5Ids.Contains(x.RoundId) && x.Hole.Par > 3 && x.Hole.Stats!.HitFairway.HasValue)
            .ToList();

        if (recent5FairwayHoles.Count > 0 && previous5FairwayHoles.Count > 0)
        {
            var recent5Fir = (double)recent5FairwayHoles.Count(x => x.Hole.Stats!.HitFairway == true) / recent5FairwayHoles.Count * 100;
            var previous5Fir = (double)previous5FairwayHoles.Count(x => x.Hole.Stats!.HitFairway == true) / previous5FairwayHoles.Count * 100;
            result.FirPercentTrend = Math.Round(recent5Fir - previous5Fir, 1);
        }

        // GIR Trend
        var recent5GreenHoles = holeStatsForTrends
            .Where(x => recent5Ids.Contains(x.RoundId) && x.Hole.Stats!.HitGreen.HasValue)
            .ToList();
        var previous5GreenHoles = holeStatsForTrends
            .Where(x => previous5Ids.Contains(x.RoundId) && x.Hole.Stats!.HitGreen.HasValue)
            .ToList();

        if (recent5GreenHoles.Count > 0 && previous5GreenHoles.Count > 0)
        {
            var recent5Gir = (double)recent5GreenHoles.Count(x => x.Hole.Stats!.HitGreen == true) / recent5GreenHoles.Count * 100;
            var previous5Gir = (double)previous5GreenHoles.Count(x => x.Hole.Stats!.HitGreen == true) / previous5GreenHoles.Count * 100;
            result.GirPercentTrend = Math.Round(recent5Gir - previous5Gir, 1);
        }

        // Putts Trend 18 Hole
        var recent5PuttHoles = holeStatsForTrends
            .Where(x => recent5FullIds.Contains(x.RoundId) && x.Hole.Stats!.NumberOfPutts.HasValue)
            .ToList();
        var previous5PuttHoles = holeStatsForTrends
            .Where(x => previous5FullIds.Contains(x.RoundId) && x.Hole.Stats!.NumberOfPutts.HasValue)
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
                result.Average18HolePuttsTrend = Math.Round(recent5AvgPutts - previous5AvgPutts, 1);
            }
        }

        // Putts Trend 9 Hole
        var recent5Putt9Holes = holeStatsForTrends
            .Where(x => recent5NineIds.Contains(x.RoundId) && x.Hole.Stats!.NumberOfPutts.HasValue)
            .ToList();
        var previous5Putt9Holes = holeStatsForTrends
            .Where(x => previous5NineIds.Contains(x.RoundId) && x.Hole.Stats!.NumberOfPutts.HasValue)
            .ToList();

        if (recent5Putt9Holes.Count > 0 && previous5Putt9Holes.Count > 0)
        {
            var recent5PuttsByRound9Hole = recent5Putt9Holes
                .GroupBy(x => x.RoundId)
                .Select(g => g.Sum(x => x.Hole.Stats!.NumberOfPutts!.Value))
                .ToList();
            var previous5PuttsByRound9Hole = previous5Putt9Holes
                .GroupBy(x => x.RoundId)
                .Select(g => g.Sum(x => x.Hole.Stats!.NumberOfPutts!.Value))
                .ToList();

            if (recent5PuttsByRound9Hole.Count > 0 && previous5PuttsByRound9Hole.Count > 0)
            {
                var recent5AvgPutts = recent5PuttsByRound9Hole.Average();
                var previous5AvgPutts = previous5PuttsByRound9Hole.Average();
                result.Average9HolePuttsTrend = Math.Round(recent5AvgPutts - previous5AvgPutts, 1);
            }
        }
    }
}
