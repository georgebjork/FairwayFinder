using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Features.Scorecards.Models.ViewModels;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.UserManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Scorecards.Controllers;

public class ScorecardController : BaseScorecardController
{
    private readonly ILogger<ScorecardController> _logger;
    private readonly IScorecardService _scorecardService;
    private readonly IUserManagementService _userManagementService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ScorecardController(ILogger<ScorecardController> logger, IScorecardService scorecardService, IUserManagementService userManagementService, UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _scorecardService = scorecardService;
        _userManagementService = userManagementService;
        _userManager = userManager;
    }

    [AllowAnonymous]
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

        var rounds = await _scorecardService.GetScorecardListByUserIdAsync(userId);
        
        var vm = new ScorecardListViewModel
        {
            Scorecards = rounds,
            Username = username
        };
        return View(vm);
    }
    
    [AllowAnonymous]
    [Route("scorecards/@{username}/round/{roundId:long}")]
    public async Task<IActionResult> ViewScorecard([FromRoute] string username, [FromRoute] long roundId)
    {
        
        var userId = await _userManagementService.GetUserIdByUsername(username);
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("User could not be found.");
            return RedirectToAction(nameof(Index), "Home");
        }
        
        var user = await _userManager.FindByIdAsync(userId);
        var scorecard = await _scorecardService.GetScorecardByRoundIdAsync(roundId);
        var scorecard_stats = await _scorecardService.GetScorecardRoundStatsByRoundIdAsync(roundId);

        if (!scorecard.Success)
        {
            SetErrorMessage("No round was returned with that Id");
            return Redirect(nameof(Index), new { username });
        }

        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }
        
        var vm = new ScorecardViewModel
        {
            Scorecard = scorecard,
            ScorecardRoundStats = scorecard_stats,
            Name = $"{user.FirstName} {user.LastName}",
            Username = username
        };
        return View(vm);
    }
}