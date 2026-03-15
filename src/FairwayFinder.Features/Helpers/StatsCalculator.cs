using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using static FairwayFinder.Features.Enums.MissType;

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
        if (rounds.Count(x => x.FullRound == fullRound) == 0) return null;
        return Math.Round(rounds.Where(x => x.FullRound == fullRound).Average(r => r.Score), 1);
    }

    /// <summary>
    /// Calculates the score trend using linear regression slope across all filtered rounds.
    /// Returns the slope (strokes per round change). Negative = improvement, Positive = regression.
    /// Rounds are expected most-recent-first; they are reversed internally so x=0 is the oldest round.
    /// </summary>
    public static double? CalculateScoreTrend(IReadOnlyList<RoundResponse> rounds, bool fullRound = true)
    {
        var filteredRounds = rounds.Where(x => x.FullRound == fullRound).ToList();
        
        if (filteredRounds.Count < 2) return null;

        // Reverse so oldest = x:0, newest = x:n-1
        var scores = filteredRounds.AsEnumerable().Reverse().Select(r => (double)r.Score).ToList();
        var (slope, _) = CalculateLinearRegression(scores);
        
        return Math.Round(slope, 2);
    }

    /// <summary>
    /// Finds the best (lowest score) round
    /// </summary>
    public static BestRound? FindBestRound(IReadOnlyList<RoundResponse> rounds, bool fullRound = true)
    {
        if (rounds.Count(x => x.FullRound == fullRound) == 0) return null;

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
    public static List<ScoreTrendPoint> BuildScoreTrend(IReadOnlyList<RoundResponse> rounds, bool fullRound = true)
    {
        var filteredRounds = rounds.Where(x => x.FullRound == fullRound);
        return filteredRounds
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
    public static List<PuttsTrendPoint> BuildPuttsTrend(IReadOnlyList<RoundResponse> rounds, bool fullRound = true)
    {
        var filteredRounds = rounds
            .Where(x => x.FullRound == fullRound && x.UsingHoleStats && x.TotalPutts > 0);
        
        return filteredRounds
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
    public static List<FirTrendPoint> BuildFirTrend(IReadOnlyList<RoundResponse> rounds)
    {
        var result = new List<FirTrendPoint>();
        
        var roundsWithStats = rounds
            .Where(x => x.UsingHoleStats)
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
    public static List<GirTrendPoint> BuildGirTrend(IReadOnlyList<RoundResponse> rounds)
    {
        var result = new List<GirTrendPoint>();
        
        var roundsWithStats = rounds
            .Where(x => x.UsingHoleStats)
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
            .Select(g =>
            {
                var fullRounds = g.Where(x => x.FullRound).Select(r => (double)r.Score).ToArray();
                var nineHoleRounds = g.Where(x => !x.FullRound).Select(r => (double)r.Score).ToArray();

                return new CourseStats
                {
                    CourseId = g.Key.CourseId,
                    CourseName = g.Key.CourseName,
                    RoundCount = g.Count(),
                    Average18HoleScore = fullRounds.Any() ? Math.Round(fullRounds.Average(), 1) : null,
                    Average9HoleScore = nineHoleRounds.Any() ? Math.Round(nineHoleRounds.Average(), 1) : null
                };
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

        // All hole stats (for FIR/GIR - these are per-hole percentages, so combine all rounds)
        var allHoleStats = roundsWithHoleStats
            .SelectMany(r => r.Holes.Select(h => new { r.RoundId, Hole = h }))
            .Where(x => x.Hole.Stats != null)
            .ToList();
        
        var allHoleStats18Holes = roundsWithHoleStats.Where(x => x.FullRound)
            .SelectMany(r => r.Holes.Select(h => new { r.RoundId, Hole = h }))
            .Where(x => x.Hole.Stats != null)
            .ToList();
        
        var allHoleStats9Holes = roundsWithHoleStats.Where(x => !x.FullRound)
            .SelectMany(r => r.Holes.Select(h => new { r.RoundId, Hole = h }))
            .Where(x => x.Hole.Stats != null)
            .ToList();
        

        // FIR % (only par 4/5 holes) - uses all rounds
        var fairwayHoles = allHoleStats
            .Where(x => x.Hole.Par > 3 && x.Hole.Stats!.HitFairway.HasValue)
            .ToList();
        
        if (fairwayHoles.Count > 0)
        {
            var fairwaysHit = fairwayHoles.Count(x => x.Hole.Stats!.HitFairway == true);
            result.FirPercent = Math.Round((double)fairwaysHit / fairwayHoles.Count * 100, 1);
        }

        // GIR % (all holes) - uses all rounds
        var greenHoles = allHoleStats
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

        // Calculate trends using linear regression (need at least 2 rounds)
        if (roundsWithHoleStats.Count >= 2)
        {
            CalculateAdvancedStatsTrends(result, roundsWithHoleStats);
        }

        return result;
    }

    /// <summary>
    /// Computes the linear regression (least-squares) for a list of y-values
    /// where x-values are sequential indices (0, 1, 2, ...).
    /// Returns (slope, intercept) for y = slope * x + intercept.
    /// </summary>
    internal static (double Slope, double Intercept) CalculateLinearRegression(IReadOnlyList<double> yValues)
    {
        var n = yValues.Count;
        if (n < 2) return (0, yValues.Count == 1 ? yValues[0] : 0);

        double sumX = 0, sumY = 0, sumXy = 0, sumX2 = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += yValues[i];
            sumXy += i * yValues[i];
            sumX2 += (double)i * i;
        }

        var denominator = n * sumX2 - sumX * sumX;
        if (denominator == 0) return (0, sumY / n);

        var slope = (n * sumXy - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / n;

        return (slope, intercept);
    }

    /// <summary>
    /// Generic trend line builder. Takes a list of y-values and date labels,
    /// computes linear regression, and returns TrendLinePoints.
    /// </summary>
    private static List<TrendLinePoint> BuildTrendLine(IReadOnlyList<double> values, IReadOnlyList<string> labels)
    {
        if (values.Count < 2) return new List<TrendLinePoint>();

        var (slope, intercept) = CalculateLinearRegression(values);

        var result = new List<TrendLinePoint>();
        for (int i = 0; i < values.Count; i++)
        {
            result.Add(new TrendLinePoint
            {
                DateLabel = i < labels.Count ? labels[i] : string.Empty,
                Value = Math.Round(slope * i + intercept, 1)
            });
        }

        return result;
    }

    /// <summary>
    /// Builds trend line for score chart. Input should be oldest-to-newest.
    /// Labels are used as-is for each point's DateLabel (to handle duplicate dates).
    /// </summary>
    public static List<TrendLinePoint> BuildScoreTrendLine(IReadOnlyList<ScoreTrendPoint> trendPoints, IReadOnlyList<string>? labels = null)
    {
        if (trendPoints.Count < 2) return new List<TrendLinePoint>();
        var values = trendPoints.Select(p => (double)p.Score).ToList();
        var defaultLabels = labels ?? trendPoints.Select(p => p.DatePlayed.ToString("M/d")).ToList();
        return BuildTrendLine(values, defaultLabels);
    }

    /// <summary>
    /// Builds trend line for putts chart. Input should be oldest-to-newest.
    /// </summary>
    public static List<TrendLinePoint> BuildPuttsTrendLine(IReadOnlyList<PuttsTrendPoint> trendPoints, IReadOnlyList<string>? labels = null)
    {
        if (trendPoints.Count < 2) return new List<TrendLinePoint>();
        var values = trendPoints.Select(p => (double)p.Putts).ToList();
        var defaultLabels = labels ?? trendPoints.Select(p => p.DatePlayed.ToString("M/d")).ToList();
        return BuildTrendLine(values, defaultLabels);
    }

    /// <summary>
    /// Builds trend line for FIR% chart. Input should be oldest-to-newest.
    /// </summary>
    public static List<TrendLinePoint> BuildFirTrendLine(IReadOnlyList<FirTrendPoint> trendPoints, IReadOnlyList<string>? labels = null)
    {
        if (trendPoints.Count < 2) return new List<TrendLinePoint>();
        var values = trendPoints.Select(p => p.FirPercent).ToList();
        var defaultLabels = labels ?? trendPoints.Select(p => p.DatePlayed.ToString("M/d")).ToList();
        return BuildTrendLine(values, defaultLabels);
    }

    /// <summary>
    /// Builds trend line for GIR% chart. Input should be oldest-to-newest.
    /// </summary>
    public static List<TrendLinePoint> BuildGirTrendLine(IReadOnlyList<GirTrendPoint> trendPoints, IReadOnlyList<string>? labels = null)
    {
        if (trendPoints.Count < 2) return new List<TrendLinePoint>();
        var values = trendPoints.Select(p => p.GirPercent).ToList();
        var defaultLabels = labels ?? trendPoints.Select(p => p.DatePlayed.ToString("M/d")).ToList();
        return BuildTrendLine(values, defaultLabels);
    }

    /// <summary>
    /// Calculates per-hole aggregate stats across all rounds at a course.
    /// Groups hole data by HoleNumber (since different teeboxes have different HoleIds
    /// but the same HoleNumber). Uses the most common par for each hole.
    /// </summary>
    public static List<HoleAggregateStats> CalculateHoleAggregateStats(IReadOnlyList<RoundResponse> rounds)
    {
        // Flatten all holes from all rounds, keeping only holes with scores
        var allHoles = rounds
            .SelectMany(r => r.Holes)
            .Where(h => h.Score.HasValue)
            .ToList();

        if (allHoles.Count == 0) return new List<HoleAggregateStats>();

        return allHoles
            .GroupBy(h => h.HoleNumber)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var holes = g.ToList();

                // Use the most common par for this hole number (handles multiple teeboxes)
                var par = holes
                    .GroupBy(h => h.Par)
                    .OrderByDescending(pg => pg.Count())
                    .First().Key;

                // Use the most common handicap for this hole number
                var handicap = holes
                    .Where(h => h.Handicap > 0)
                    .GroupBy(h => h.Handicap)
                    .OrderByDescending(hg => hg.Count())
                    .Select(hg => hg.Key)
                    .FirstOrDefault();

                var avgYardage = (int)Math.Round(holes.Average(h => h.Yardage));
                var avgScore = Math.Round((decimal)holes.Average(h => h.Score!.Value), 2);

                var result = new HoleAggregateStats
                {
                    HoleNumber = g.Key,
                    Par = par,
                    Handicap = handicap,
                    AverageYardage = avgYardage,
                    TimesPlayed = holes.Count,
                    AverageScore = avgScore,
                    AverageScoreToPar = Math.Round(avgScore - par, 2)
                };

                // Fairway stats (only meaningful for par 4/5)
                if (par > 3)
                {
                    var fairwayHoles = holes
                        .Where(h => h.Stats?.HitFairway.HasValue == true)
                        .ToList();

                    if (fairwayHoles.Count > 0)
                    {
                        var hit = fairwayHoles.Count(h => h.Stats!.HitFairway == true);
                        result.FairwayHitPercent = Math.Round((decimal)hit / fairwayHoles.Count * 100, 1);

                        // Fairway miss breakdown (only count actual misses)
                        var fairwayMisses = fairwayHoles
                            .Where(h => h.Stats!.HitFairway == false && h.Stats!.MissFairwayType.HasValue)
                            .Select(h => h.Stats!.MissFairwayType!.Value)
                            .ToList();

                        if (fairwayMisses.Count > 0)
                        {
                            result.FairwayMiss = BuildMissBreakdown(fairwayMisses);
                        }
                    }
                }

                // GIR stats
                var girHoles = holes
                    .Where(h => h.Stats?.HitGreen.HasValue == true)
                    .ToList();

                if (girHoles.Count > 0)
                {
                    var hit = girHoles.Count(h => h.Stats!.HitGreen == true);
                    result.GirPercent = Math.Round((decimal)hit / girHoles.Count * 100, 1);

                    // Green miss breakdown
                    var greenMisses = girHoles
                        .Where(h => h.Stats!.HitGreen == false && h.Stats!.MissGreenType.HasValue)
                        .Select(h => h.Stats!.MissGreenType!.Value)
                        .ToList();

                    if (greenMisses.Count > 0)
                    {
                        result.GreenMiss = BuildMissBreakdown(greenMisses);
                    }
                }

                // Putting stats
                var puttHoles = holes
                    .Where(h => h.Stats?.NumberOfPutts.HasValue == true)
                    .ToList();

                if (puttHoles.Count > 0)
                {
                    result.AveragePutts = Math.Round((decimal)puttHoles.Average(h => h.Stats!.NumberOfPutts!.Value), 2);
                }

                return result;
            })
            .ToList();
    }

    /// <summary>
    /// Builds a miss direction breakdown from a list of miss type IDs.
    /// IDs: 1=Left, 2=Right, 3=Short, 4=Long, 5=None.
    /// </summary>
    private static MissBreakdown BuildMissBreakdown(List<long> missTypeIds)
    {
        // Filter out "None" (ID 5)
        var actualMisses = missTypeIds.Where(id => id != 5).ToList();

        return new MissBreakdown
        {
            LeftCount = actualMisses.Count(id => id.Is(MissTypeEnum.MissLeft)),
            RightCount = actualMisses.Count(id => id.Is(MissTypeEnum.MissRight)),
            ShortCount = actualMisses.Count(id => id.Is(MissTypeEnum.MissShort)),
            LongCount = actualMisses.Count(id => id.Is(MissTypeEnum.MissLong)),
            OtherCount = actualMisses.Count(id => id.Is(MissTypeEnum.MissOther)),
            TotalMisses = actualMisses.Count
        };
    }

    private static void CalculateAdvancedStatsTrends(
        AdvancedStats result,
        List<RoundResponse> roundsWithHoleStats)
    {
        // FIR% Trend — compute per-round FIR%, then regress (oldest first)
        var firPerRound = roundsWithHoleStats
            .AsEnumerable().Reverse() // oldest first
            .Select(r =>
            {
                var fairwayHoles = r.Holes.Where(h => h.Par > 3 && h.Stats?.HitFairway.HasValue == true).ToList();
                if (fairwayHoles.Count == 0) return (double?)null;
                return (double)fairwayHoles.Count(h => h.Stats!.HitFairway == true) / fairwayHoles.Count * 100;
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        if (firPerRound.Count >= 2)
        {
            var (slope, _) = CalculateLinearRegression(firPerRound);
            result.FirPercentTrend = Math.Round(slope, 2);
        }

        // GIR% Trend — compute per-round GIR%, then regress (oldest first)
        var girPerRound = roundsWithHoleStats
            .AsEnumerable().Reverse()
            .Select(r =>
            {
                var greenHoles = r.Holes.Where(h => h.Stats?.HitGreen.HasValue == true).ToList();
                if (greenHoles.Count == 0) return (double?)null;
                return (double)greenHoles.Count(h => h.Stats!.HitGreen == true) / greenHoles.Count * 100;
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        if (girPerRound.Count >= 2)
        {
            var (slope, _) = CalculateLinearRegression(girPerRound);
            result.GirPercentTrend = Math.Round(slope, 2);
        }

        // Putts Trend 18 Hole — total putts per round, then regress (oldest first)
        var putts18PerRound = roundsWithHoleStats
            .Where(x => x.FullRound)
            .AsEnumerable().Reverse()
            .Select(r =>
            {
                var putts = r.Holes.Where(h => h.Stats?.NumberOfPutts.HasValue == true).Sum(h => h.Stats!.NumberOfPutts!.Value);
                return putts > 0 ? (double?)putts : null;
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        if (putts18PerRound.Count >= 2)
        {
            var (slope, _) = CalculateLinearRegression(putts18PerRound);
            result.Average18HolePuttsTrend = Math.Round(slope, 2);
        }

        // Putts Trend 9 Hole — total putts per round, then regress (oldest first)
        var putts9PerRound = roundsWithHoleStats
            .Where(x => !x.FullRound)
            .AsEnumerable().Reverse()
            .Select(r =>
            {
                var putts = r.Holes.Where(h => h.Stats?.NumberOfPutts.HasValue == true).Sum(h => h.Stats!.NumberOfPutts!.Value);
                return putts > 0 ? (double?)putts : null;
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        if (putts9PerRound.Count >= 2)
        {
            var (slope, _) = CalculateLinearRegression(putts9PerRound);
            result.Average9HolePuttsTrend = Math.Round(slope, 2);
        }
    }
}
