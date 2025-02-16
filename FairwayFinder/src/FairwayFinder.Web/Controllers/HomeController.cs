using System.Diagnostics;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Mvc;
using FairwayFinder.Web.Models;

namespace FairwayFinder.Web.Controllers;


public class HomeController : BaseAuthorizedController
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly ScorecardService _scorecardService;

    public HomeController(ILogger<HomeController> logger, IUsernameRetriever usernameRetriever, ScorecardService scorecardService)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _scorecardService = scorecardService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> GetMostRecentRounds()
    {
        var user_id = _usernameRetriever.UserId;
        
        var rounds = await _scorecardService.GetRecentScorecardsByUserIdAsync(user_id, 20);

        // Helps pass the username into the view 
        ViewBag.Username = _usernameRetriever.Username;
        return PartialView("_RoundsTable", rounds);
    }
}