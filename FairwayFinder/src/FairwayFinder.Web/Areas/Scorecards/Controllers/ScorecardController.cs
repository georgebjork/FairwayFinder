using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.ViewModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Scorecards.Controllers;

public class ScorecardController : BaseScorecardController
{
    private readonly ILogger<ScorecardController> _logger;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly CourseLookupService _courseLookupService;
    private readonly TeeboxLookupService _teeboxLookupService;

    public ScorecardController(ILogger<ScorecardController> logger, IUsernameRetriever usernameRetriever, CourseLookupService courseLookupService, TeeboxLookupService teeboxLookupService)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _courseLookupService = courseLookupService;
        _teeboxLookupService = teeboxLookupService;
    }

    [Route("scorecards/@{username}")]
    public IActionResult Index([FromRoute] string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        } 
        
        var vm = new ScorecardsViewModel();
        return View(vm);
    }

    [HttpGet]
    [Route("scorecards/add")]
    public IActionResult AddRound()
    {
        var form = new ScorecardFormModel();
        return View(form);
    }

    [HttpGet]
    [Route("/scorecards/course-search")]
    public async Task<IActionResult> SearchForCourseHtmx([FromQuery] string? course)
    {
        if (course is null)
        {
            return PartialView("_CourseSearchResults", new List<Course>());
        }
        
        var course_results = await _courseLookupService.CourseSearchByName(course);
        return PartialView("_CourseSearchResults", course_results);
    }
    
    [HttpGet]
    [Route("/scorecards/get-course-data/{courseId:long}")]
    public async Task<IActionResult> GetCourseAndTeeboxesForRoundForm([FromRoute] long courseId)
    {
        var course = await _courseLookupService.GetCourseByIdAsync(courseId);
        if (course is null)
        {
            SetErrorMessage("Error occurred getting data for course.");
            return Redirect(nameof(AddRound));
        }

        var teeboxes = await _teeboxLookupService.GetTeesDropdownForCourseAsync(courseId);
        return PartialView("_RoundForm", new ScorecardFormModel
        {
            Course = course,
            TeeboxeSelectList = teeboxes
        });
    }
}