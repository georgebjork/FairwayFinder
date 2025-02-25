using System.Diagnostics;
using System.Text.Json;
using FairwayFinder.Core.Features.Dashboard.Models.ViewModel;
using FairwayFinder.Core.Features.Dashboard.Services;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Mvc;
using FairwayFinder.Web.Models;

namespace FairwayFinder.Web.Controllers;


public class HomeController : BaseAuthorizedController
{
    private readonly ILogger<HomeController> _logger;
    private readonly DashboardService _dashboardService;

    public HomeController(ILogger<HomeController> logger, IUsernameRetriever usernameRetriever, DashboardService dashboardService)
    {
        _logger = logger;
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel();

        var year_filters = await _dashboardService.GetYearFilters();
        vm.YearFilters = year_filters;
        
        return View(vm);
    }

    public async Task<IActionResult> GetRounds()
    {
        var vm = await _dashboardService.GetRoundsListAsync();
        
        SendHtmxTriggerAfterSettle(HtmxTriggers.RenderTable);
        return PartialView("Shared/_DashboardRoundsTable", vm);
    }

    public async Task<IActionResult> GetHoleScoreStats()
    {
        var vm = await _dashboardService.GetHoleScoreStats();
        return PartialView("Shared/_RoundStatsDashboard", vm);
    }

    public async Task<IActionResult> GetHeaderCardsData()
    {
        var vm = await _dashboardService.GetHeaderCardsViewModel();
        return PartialView("Shared/_DashboardHeaderCardStats", vm);
    }
    
    public async Task<IActionResult> GetScoresChartData()
    {
        var vm = await _dashboardService.GetRoundScoresChartViewModel();
        
        SendHtmxTriggerAfterSettle(HtmxTriggers.RenderChart);
        return PartialView("Shared/_DashboardScoresLineChart", vm);
    }
}