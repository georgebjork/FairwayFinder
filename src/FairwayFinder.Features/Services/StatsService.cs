using FairwayFinder.Features.Data;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Features.Services;

public class StatsService : IStatsService
{
    private readonly IRoundService _roundService;

    public StatsService(IRoundService roundService)
    {
        _roundService = roundService;
    }

    public async Task<UserStatsResponse> GetUserStatsAsync(string userId, StatsFilter? filter = null, int coursesCount = 5)
    {
        var rounds = await _roundService.GetRoundsWithDetailsAsync(userId);
        
        var statsRounds = rounds.Where(r => !r.ExcludeFromStats).ToList();
        
        // Apply filters
        statsRounds = ApplyFilters(statsRounds, filter);
        
        if (statsRounds.Count == 0)
        {
            return new UserStatsResponse();
        }

        var response = new UserStatsResponse
        {
            TotalRounds = statsRounds.Count,
            Total18HoleRounds = statsRounds.Count(x => x.FullRound),
            Total9HoleRounds = statsRounds.Count(x => !x.FullRound),
            Average18HoleScore = StatsCalculator.CalculateAverageScore(statsRounds),
            Average9HoleScore = StatsCalculator.CalculateAverageScore(statsRounds, false),
            Average18HoleScoreTrend = StatsCalculator.CalculateScoreTrend(statsRounds),
            Average9HoleScoreTrend = StatsCalculator.CalculateScoreTrend(statsRounds, fullRound: false),
            Best18HoleRound = StatsCalculator.FindBestRound(statsRounds),
            Best9HoleRound = StatsCalculator.FindBestRound(statsRounds, false),
            ScoreTrend18Hole = StatsCalculator.BuildScoreTrend(statsRounds),
            ScoreTrend9Hole = StatsCalculator.BuildScoreTrend(statsRounds, fullRound: false),
            PuttsTrend18Hole = StatsCalculator.BuildPuttsTrend(statsRounds),
            PuttsTrend9Hole = StatsCalculator.BuildPuttsTrend(statsRounds, fullRound: false),
            FirTrend = StatsCalculator.BuildFirTrend(statsRounds),
            GirTrend = StatsCalculator.BuildGirTrend(statsRounds),
            MostPlayedCourses = StatsCalculator.CalculateCourseStats(statsRounds, coursesCount),
            ScoringDistribution = StatsCalculator.AggregateScoringDistribution(statsRounds),
            ParTypeScoring = StatsCalculator.CalculateParTypeScoring(statsRounds),
            AdvancedStats = StatsCalculator.CalculateAdvancedStats(statsRounds),
            Rounds = statsRounds
        };

        // Strokes Gained — from stored values on RoundResponse
        var roundsWithSg = statsRounds.Where(r => r.StrokesGained != null).ToList();
        if (roundsWithSg.Count > 0)
        {
            response.StrokesGained = AggregateStoredSg(roundsWithSg);
            response.SgTotalTrend = BuildSgTrendFromStored(roundsWithSg, sg => sg.SgTotal);
            response.SgPuttingTrend = BuildSgTrendFromStored(roundsWithSg, sg => sg.SgPutting);
            response.SgTeeToGreenTrend = BuildSgTrendFromStored(roundsWithSg, sg => sg.SgTeeToGreen);
        }

        return response;
    }
    
    public async Task<CourseStatsResponse?> GetCourseStatsAsync(string userId, long courseId, long? teeboxId = null, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var rounds = await _roundService.GetRoundsWithDetailsAsync(userId);
        
        var allCourseRounds = rounds
            .Where(r => r.CourseId == courseId && !r.ExcludeFromStats)
            .ToList();
        
        if (allCourseRounds.Count == 0)
        {
            return null;
        }

        // Build teebox options from all rounds at this course (before any filtering)
        var teeboxOptions = allCourseRounds
            .GroupBy(r => new { r.Teebox.TeeboxId, r.Teebox.TeeboxName })
            .OrderByDescending(g => g.Count())
            .Select(g => new CourseTeeboxOption
            {
                TeeboxId = g.Key.TeeboxId,
                TeeboxName = g.Key.TeeboxName,
                RoundCount = g.Count()
            })
            .ToList();

        // Apply teebox filter if specified
        var filteredRounds = teeboxId.HasValue
            ? allCourseRounds.Where(r => r.Teebox.TeeboxId == teeboxId.Value).ToList()
            : allCourseRounds;
        
        // Apply date range filter
        if (startDate.HasValue)
        {
            filteredRounds = filteredRounds
                .Where(r => r.DatePlayed >= startDate.Value)
                .ToList();
        }
        if (endDate.HasValue)
        {
            filteredRounds = filteredRounds
                .Where(r => r.DatePlayed <= endDate.Value)
                .ToList();
        }
        
        if (filteredRounds.Count == 0)
        {
            // Teebox filter matched no rounds — return shell with options so user can pick another
            return new CourseStatsResponse
            {
                CourseId = courseId,
                CourseName = allCourseRounds.First().CourseName,
                TeeboxOptions = teeboxOptions,
                SelectedTeeboxId = teeboxId
            };
        }

        var courseName = filteredRounds.First().CourseName;

        var response = new CourseStatsResponse
        {
            CourseId = courseId,
            CourseName = courseName,
            TotalRounds = filteredRounds.Count,
            Average18HoleScore = StatsCalculator.CalculateAverageScore(filteredRounds),
            Average9HoleScore = StatsCalculator.CalculateAverageScore(filteredRounds, false),
            Best18HoleRound = StatsCalculator.FindBestRound(filteredRounds),
            Best9HoleRound = StatsCalculator.FindBestRound(filteredRounds, false),
            HoleStats = StatsCalculator.CalculateHoleAggregateStats(filteredRounds),
            ScoringDistribution = StatsCalculator.AggregateScoringDistribution(filteredRounds),
            Rounds = filteredRounds,
            TeeboxOptions = teeboxOptions,
            SelectedTeeboxId = teeboxId
        };

        // Strokes Gained from stored values
        var roundsWithSg = filteredRounds.Where(r => r.StrokesGained != null).ToList();
        if (roundsWithSg.Count > 0)
        {
            response.StrokesGained = AggregateStoredSg(roundsWithSg);
        }

        return response;
    }
    
    public async Task<List<int>> GetAvailableYearsAsync(string userId)
    {
        var rounds = await _roundService.GetRoundsByUserIdAsync(userId);
        
        return rounds
            .Select(r => r.DatePlayed.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
    }
    
    public async Task<List<CourseOption>> GetUserCoursesAsync(string userId)
    {
        var courses = await _roundService.GetPlayedCoursesByUserId(userId, true);

        return courses.Select(x => new CourseOption
        {
            CourseId = x.CourseId,
            CourseName = x.CourseName
        }).ToList();
    }
    
    private static List<RoundResponse> ApplyFilters(List<RoundResponse> rounds, StatsFilter? filter)
    {
        if (filter is null || !filter.HasFilters)
        {
            return rounds;
        }
        
        var result = rounds.AsEnumerable();
        
        // Filter by round type (9 or 18 hole)
        if (filter.FullRoundOnly.HasValue)
        {
            result = result.Where(r => r.FullRound == filter.FullRoundOnly.Value);
        }
        
        // Filter by date range
        if (filter.StartDate.HasValue)
        {
            result = result.Where(r => r.DatePlayed >= filter.StartDate.Value);
        }
        
        if (filter.EndDate.HasValue)
        {
            result = result.Where(r => r.DatePlayed <= filter.EndDate.Value);
        }
        
        // Filter by course
        if (filter.CourseId.HasValue)
        {
            result = result.Where(r => r.CourseId == filter.CourseId.Value);
        }
        
        return result.ToList();
    }

    /// <summary>
    /// Aggregates stored SG values from multiple rounds into an average summary.
    /// </summary>
    private static StrokesGainedSummary AggregateStoredSg(List<RoundResponse> rounds)
    {
        var count = rounds.Count;
        var summary = new StrokesGainedSummary
        {
            RoundsIncluded = count,
            HolesWithShots = rounds.Sum(r => r.StrokesGained!.HolesWithShots),
            SgTotal = Math.Round(rounds.Average(r => r.StrokesGained!.SgTotal), 2),
            SgPutting = Math.Round(rounds.Average(r => r.StrokesGained!.SgPutting), 2),
            SgTeeToGreen = Math.Round(rounds.Average(r => r.StrokesGained!.SgTeeToGreen), 2),
            SgOffTheTee = Math.Round(rounds.Average(r => r.StrokesGained!.SgOffTheTee), 2),
            SgApproach = Math.Round(rounds.Average(r => r.StrokesGained!.SgApproach), 2),
            SgAroundTheGreen = Math.Round(rounds.Average(r => r.StrokesGained!.SgAroundTheGreen), 2)
        };

        if (count >= 3)
        {
            var ordered = rounds.OrderBy(r => r.DatePlayed).Select(r => r.StrokesGained!).ToList();
            summary.SgTotalTrend = CalcSlope(ordered.Select(s => s.SgTotal).ToList());
            summary.SgPuttingTrend = CalcSlope(ordered.Select(s => s.SgPutting).ToList());
            summary.SgTeeToGreenTrend = CalcSlope(ordered.Select(s => s.SgTeeToGreen).ToList());
            summary.SgOffTheTeeTrend = CalcSlope(ordered.Select(s => s.SgOffTheTee).ToList());
            summary.SgApproachTrend = CalcSlope(ordered.Select(s => s.SgApproach).ToList());
            summary.SgAroundTheGreenTrend = CalcSlope(ordered.Select(s => s.SgAroundTheGreen).ToList());
        }

        return summary;
    }

    /// <summary>
    /// Builds SG trend points from stored per-round values.
    /// </summary>
    private static List<StrokesGainedTrendPoint> BuildSgTrendFromStored(
        List<RoundResponse> rounds, Func<StrokesGainedSummary, double> valueSelector)
    {
        var points = rounds
            .OrderBy(r => r.DatePlayed)
            .Select(r => new StrokesGainedTrendPoint
            {
                RoundId = r.RoundId,
                DatePlayed = r.DatePlayed,
                CourseName = r.CourseName,
                Value = Math.Round(valueSelector(r.StrokesGained!), 2)
            })
            .ToList();

        // 3-round moving average
        for (int i = 2; i < points.Count; i++)
        {
            points[i].MovingAverage = Math.Round(
                (points[i].Value + points[i - 1].Value + points[i - 2].Value) / 3.0, 2);
        }

        return points;
    }

    private static double? CalcSlope(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return null;
        var (slope, _) = StatsCalculator.CalculateLinearRegression(values);
        return Math.Round(slope, 3);
    }
}
