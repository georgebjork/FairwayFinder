using System.Diagnostics;
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

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> GetRounds()
    {
        var vm = await _dashboardService.GetRoundsListAsync(20);
        ViewBag.Username = vm.Username;
        return PartialView("_RoundsTable", vm.Rounds);
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
}