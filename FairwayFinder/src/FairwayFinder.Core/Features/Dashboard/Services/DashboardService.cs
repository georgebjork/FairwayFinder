using FairwayFinder.Core.Features.Dashboard.Models.ViewModel;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Dashboard.Services;

public class DashboardService
{
    private readonly ILogger<DashboardService> _logger;
    private readonly IScorecardRepository _scorecardRepository;
    private readonly IUsernameRetriever _usernameRetriever;

    public DashboardService(ILogger<DashboardService> logger, IUsernameRetriever usernameRetriever, IScorecardRepository scorecardRepository)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _scorecardRepository = scorecardRepository;
    }

    public async Task<RoundListViewModel> GetRoundsListAsync(int limit)
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

        var round_stats = await _scorecardRepository.GetRoundStatsListAsync(userId);

        return new RoundStatsViewModel
        {
            RoundStatsList = round_stats
        };
    }
}