using FairwayFinder.Core.Features.Dashboard.Models;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Stats;
using FairwayFinder.Core.Stats.Models.QueryModels;
using FairwayFinder.Core.Stats.Repositories;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Dashboard.Services;

public class DashboardService
{
    private readonly ILogger<DashboardService> _logger;
    private readonly IScorecardRepository _scorecardRepository;
    private readonly IAggregatedStatRepository _aggregatedStatRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly IUsernameRetriever _usernameRetriever;

    public DashboardService(ILogger<DashboardService> logger, IUsernameRetriever usernameRetriever, IScorecardRepository scorecardRepository, IAggregatedStatRepository aggregatedStatRepository, ILookupRepository lookupRepository)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _scorecardRepository = scorecardRepository;
        _aggregatedStatRepository = aggregatedStatRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<Dictionary<long, string>> GetYearFilters()
    {
        var userId = _usernameRetriever.UserId;
        var years = await _lookupRepository.GetDistinctYearsFromRoundsAsync(userId);
        return years;
    }

    public async Task<List<RoundsQueryModel>> GetRoundsByUserIdAsync(string userId, StatsRequest filters)
    {
        var rounds = await _aggregatedStatRepository.GetRoundsByUserId(userId, filters);
        return rounds;
    }

    public async Task<RoundScoreStatsQueryModel> GetHoleScoreStats()
    {
        var userId = _usernameRetriever.UserId;

        var round_stats = await _aggregatedStatRepository.GetScoreStatsByUserIdAsync(userId);
        return round_stats;
    }
    
    public async Task<RoundScoresSummaryResponse> GetRoundScoresSummaryByUserId(string userId, StatsRequest request)
    {
        var round_count = await _aggregatedStatRepository.GetNumberOfRoundsPlayedAsync(userId, request);
        var avg_score = await _aggregatedStatRepository.GetAverageScoreOfRoundsAsync(userId, request);
        var low_score = await _aggregatedStatRepository.GetLowScoreOfRoundsAsync(userId, request);

        
        return new RoundScoresSummaryResponse
        {
            AvgScore = Math.Round(avg_score, 2),
            RoundsPlayed = round_count,
            LowRound = low_score
        };
    }
}