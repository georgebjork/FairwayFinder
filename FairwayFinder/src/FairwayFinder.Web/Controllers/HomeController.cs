using FairwayFinder.Core.Features.Dashboard.Models.ViewModel;
using FairwayFinder.Core.Features.Dashboard.Services;
using FairwayFinder.Core.HttpClients;
using FairwayFinder.Core.HttpClients.UploadThing;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Stats;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Controllers;


public class HomeController : BaseAuthorizedController
{
    private readonly DashboardService _dashboardService;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly UploadThingHttpClient _httpClient;
    
    public HomeController(ILogger<HomeController> logger, IUsernameRetriever usernameRetriever, DashboardService dashboardService, UploadThingHttpClient httpClient)
    {
        _usernameRetriever = usernameRetriever;
        _dashboardService = dashboardService;
        _httpClient = httpClient;
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

        if (!IsHtmx() || IsBoosted()) return View(vm);
        
        SendHtmxTriggerAfterSettle(HtmxTriggers.RenderDashboard);
        return PartialView(vm);

    }
    
    [Route("file-test")]
    [HttpGet]
    public async Task<IActionResult> TestFileUpload()
    {
        return View();
    }
    
    [Route("file-test")]
    [HttpPost]
    public async Task<IActionResult> TestFileUpload(IFormFile file)
    {
        await _httpClient.Upload(file.OpenReadStream(), file.FileName, file.ContentType);
        return Redirect(nameof(TestFileUpload));
    }
}