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

    public async Task<UserStatsResponse> GetUserStatsAsync(string userId, StatsFilter? filter = null, int trendCount = 20, int coursesCount = 5)
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
            ScoreTrend18Hole = StatsCalculator.BuildScoreTrend(statsRounds, trendCount),
            ScoreTrend9Hole = StatsCalculator.BuildScoreTrend(statsRounds, trendCount, fullRound: false),
            PuttsTrend18Hole = StatsCalculator.BuildPuttsTrend(statsRounds, trendCount),
            FirTrend = StatsCalculator.BuildFirTrend(statsRounds, trendCount),
            GirTrend = StatsCalculator.BuildGirTrend(statsRounds, trendCount),
            MostPlayedCourses = StatsCalculator.CalculateCourseStats(statsRounds, coursesCount),
            ScoringDistribution = StatsCalculator.AggregateScoringDistribution(statsRounds),
            ParTypeScoring = StatsCalculator.CalculateParTypeScoring(statsRounds),
            AdvancedStats = StatsCalculator.CalculateAdvancedStats(statsRounds),
            Rounds = statsRounds
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
        
        return result.ToList();
    }
}
