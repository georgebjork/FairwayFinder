using FairwayFinder.Core.Features.Dashboard.Models.ViewModel;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Features.Stats.Repositories;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Dashboard.Services;

public class DashboardService
{
    private readonly ILogger<DashboardService> _logger;
    private readonly IScorecardRepository _scorecardRepository;
    private readonly IStatRepository _statRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly IUsernameRetriever _usernameRetriever;

    public DashboardService(ILogger<DashboardService> logger, IUsernameRetriever usernameRetriever, IScorecardRepository scorecardRepository, IStatRepository statRepository, ILookupRepository lookupRepository)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _scorecardRepository = scorecardRepository;
        _statRepository = statRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<Dictionary<long, string>> GetYearFilters()
    {
        var userId = _usernameRetriever.UserId;
        var years = await _lookupRepository.GetDistinctYearsFromRoundsAsync(userId);
        return years;
    }

    public async Task<RoundListViewModel> GetRoundsListAsync(int? limit = null)
    {
        var userId = _usernameRetriever.UserId;
        var username = _usernameRetriever.Username;
        
        var rounds = await _scorecardRepository.GetRoundsSummaryByUserIdAsync(userId, limit);

        return new RoundListViewModel
        {
            Rounds = rounds,
            Username = username
        };
    }

    public async Task<RoundStatsViewModel> GetHoleScoreStats()
    {
        var userId = _usernameRetriever.UserId;

        var round_stats = await _statRepository.GetScoreStatsByUserIdAsync(userId);

        return new RoundStatsViewModel
        {
            ScoreStatsQueryModel = round_stats
        };
    }
    
    public async Task<DashboardHeaderCardsViewModel> GetHeaderCardsViewModel()
    {
        var userId = _usernameRetriever.UserId;

        var round_count = await _statRepository.GetNumberOfRoundsPlayedAsync(userId);
        var avg_score = await _statRepository.GetAverageScoreOfRoundsAsync(userId);
        var low_score = await _statRepository.GetLowScoreOfRoundsAsync(userId);


        return new DashboardHeaderCardsViewModel
        {
            AvgScore = Math.Round(avg_score, 2),
            RoundsPlayed = round_count,
            LowRound = low_score
        };
    }

    public async Task<DashboardScoresChartViewModel> GetRoundScoresChartViewModel()
    {
        var userId = _usernameRetriever.UserId;
        var scores = await _statRepository.GetRoundScoresByUserId(userId);

        return new DashboardScoresChartViewModel
        {
            Scores = scores
        };
    }
}