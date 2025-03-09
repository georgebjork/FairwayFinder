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
        var userId = _usernameRetriever.UserId;
        var username = _usernameRetriever.Username;

        var sr = new StatsRequest { Year = year };

        var year_filters = await _dashboardService.GetYearFilters();
        var card_data = await _dashboardService.GetRoundScoresSummaryByUserId(userId, sr);
        var rounds = await _dashboardService.GetRoundsByUserIdAsync(userId, sr);


        var vm = new DashboardViewModel
        {
            Rounds = rounds,
            CardData = card_data,
            Username = username,
            YearFilters = year_filters,
            Filters =
            {
                Year = year
            }
        };

        if (!IsHtmx()) return View(vm);
        
        SendHtmxTriggerAfterSettle(HtmxTriggers.RenderDashboard);
        return PartialView(vm);

    }
}