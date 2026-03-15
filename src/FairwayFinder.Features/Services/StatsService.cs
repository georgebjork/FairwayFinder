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

        return new UserStatsResponse
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
    }
    
    public async Task<CourseStatsResponse?> GetCourseStatsAsync(string userId, long courseId, long? teeboxId = null)
    {
        var rounds = await _roundService.GetRoundsWithDetailsAsync(userId);
        
        var allCourseRounds = rounds
            .Where(r => r.CourseId == courseId && !r.ExcludeFromStats)
            .ToList();
        
        if (allCourseRounds.Count == 0)
        {
            return null;
        }

        // Build teebox options from all rounds at this course (before filtering)
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

        return new CourseStatsResponse
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
}
