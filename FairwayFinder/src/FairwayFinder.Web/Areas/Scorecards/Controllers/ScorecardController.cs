using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.ViewModels;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.UserManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Scorecards.Controllers;

public class ScorecardController : BaseScorecardController
{
    private readonly ILogger<ScorecardController> _logger;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly ScorecardService _scorecardService;
    private readonly IUserManagementService _userManagementService;

    public ScorecardController(ILogger<ScorecardController> logger, IUsernameRetriever usernameRetriever, ScorecardService scorecardService, IUserManagementService userManagementService)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _scorecardService = scorecardService;
        _userManagementService = userManagementService;
    }

    [Route("scorecards/@{username}")]
    public async Task<IActionResult> Index([FromRoute] string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        } 
        
        // Get userId by username
        var userId = await _userManagementService.GetUserIdByUsername(username);

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("User could not be found.");
            return RedirectToAction(nameof(Index), "Home");
        }

        var rounds = await _scorecardService.GetScorecardSummaryByUserIdAsync(userId);
        
        var vm = new ScorecardsViewModel
        {
            Rounds = rounds
        };
        return View(vm);
    }
}