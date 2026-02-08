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

    public async Task<UserStatsResponse> GetUserStatsAsync(string userId, int trendCount = 20, int coursesCount = 5)
    {
        var rounds = await _roundService.GetRoundsWithDetailsAsync(userId);
        
        var statsRounds = rounds.Where(r => !r.ExcludeFromStats).ToList();
        
        if (statsRounds.Count == 0)
        {
            return new UserStatsResponse();
        }

        return new UserStatsResponse
        {
            TotalRounds = statsRounds.Count,
            AverageScore = StatsCalculator.CalculateAverageScore(statsRounds),
            AverageScoreTrend = StatsCalculator.CalculateScoreTrend(statsRounds),
            BestRound = StatsCalculator.FindBestRound(statsRounds),
            ScoreTrend = StatsCalculator.BuildScoreTrend(statsRounds, trendCount),
            MostPlayedCourses = StatsCalculator.CalculateCourseStats(statsRounds, coursesCount),
            ScoringDistribution = StatsCalculator.AggregateScoringDistribution(statsRounds),
            ParTypeScoring = StatsCalculator.CalculateParTypeScoring(statsRounds),
            AdvancedStats = StatsCalculator.CalculateAdvancedStats(statsRounds)
        };
    }
}
