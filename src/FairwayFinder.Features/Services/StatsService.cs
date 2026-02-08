using FairwayFinder.Features.Data;
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
        
        return StatsCalculator.Calculate(rounds, trendCount, coursesCount);
    }
}
