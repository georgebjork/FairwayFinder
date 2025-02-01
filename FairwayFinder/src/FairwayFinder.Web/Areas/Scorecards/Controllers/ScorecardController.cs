using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.ViewModels;
using FairwayFinder.Core.Services;
using FairwayFinder.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Scorecards.Controllers;

public class ScorecardController : BaseScorecardController
{
    private readonly ILogger<ScorecardController> _logger;
    private readonly IUsernameRetriever _usernameRetriever;

    public ScorecardController(ILogger<ScorecardController> logger, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
    }

    [Route("scorecards/@{username}")]
    public IActionResult Index([FromRoute] string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        } 
        
        var vm = new ScorecardsViewModel();
        return View();
    }

    [HttpGet]
    [Route("scorecards/add")]
    public IActionResult AddRound()
    {
        var form = new RoundFormModel();
        return View(form);
    }
}