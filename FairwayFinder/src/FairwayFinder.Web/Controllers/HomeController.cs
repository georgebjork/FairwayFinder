using FairwayFinder.Core.Features.Dashboard.Models.ViewModel;
using FairwayFinder.Core.Features.Dashboard.Services;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Stats;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Controllers;


public class HomeController : BaseAuthorizedController
{
    private readonly ILogger<HomeController> _logger;
    private readonly DashboardService _dashboardService;
    private readonly IUsernameRetriever _usernameRetriever;

    public HomeController(ILogger<HomeController> logger, IUsernameRetriever usernameRetriever, DashboardService dashboardService)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index([FromQuery] long? year = null)
    {
        var vm = new DashboardViewModel();

        var year_filters = await _dashboardService.GetYearFilters();
        vm.YearFilters = year_filters;
        vm.Filters.Year = year;
        
        SendHtmxTriggerAfterSettle(HtmxTriggers.RenderChart);
        
        return View(vm);
    }

    public IActionResult ReRenderDashboard([FromQuery] long? year = null)
    {
        SendHtmxTriggerAfterSettle(HtmxTriggers.RenderDashboard);
        return Ok();
    }
    
    public async Task<IActionResult> GetHoleScoreStats()
    {
        var hole_stats = await _dashboardService.GetHoleScoreStats();
        return PartialView("Shared/_RoundStatsDashboard", new RoundStatsViewModel
        {
            ScoreStatsQueryModel = hole_stats
        });
    }

    public async Task<IActionResult> GetHeaderCardsData([FromQuery] StatsRequest filters)
    {
        var userId = _usernameRetriever.UserId;
        
        var response = await _dashboardService.GetRoundScoresSummaryByUserId(userId, filters);
        return PartialView("Shared/_DashboardHeaderCardStats", new DashboardHeaderCardsViewModel
        {
            SummaryResponse = response
        });
    }
    
    public async Task<IActionResult> GetRounds([FromQuery] StatsRequest filters)
    {
        var userId = _usernameRetriever.UserId;
        var rounds = await _dashboardService.GetRoundsByUserIdAsync(userId, filters);
        
        SendHtmxTriggerAfterSettle(HtmxTriggers.RenderTable);
        return PartialView("Shared/_DashboardRoundsTable", new DashboardRoundsTableViewModel
        {
            Rounds = rounds,
            Username = _usernameRetriever.Username
        });
    }
    
    public async Task<IActionResult> GetScoresChartData([FromQuery] StatsRequest filters)
    {
        var userId = _usernameRetriever.UserId;
        var rounds = await _dashboardService.GetRoundsByUserIdAsync(userId, filters);
        
        return PartialView("Shared/_DashboardScoresLineData", new DashboardScoresChartViewModel
        {
            Rounds = rounds
        });
    }
}