using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Scorecards.Controllers;

public class RoundController : BaseScorecardController
{
    private readonly ILogger<RoundController> _logger;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly CourseLookupService _courseLookupService;
    private readonly TeeboxLookupService _teeboxLookupService;
    private readonly HoleLookupService _holeLookupService;

    public RoundController(ILogger<RoundController> logger, IUsernameRetriever usernameRetriever, CourseLookupService courseLookupService, TeeboxLookupService teeboxLookupService, HoleLookupService holeLookupService)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _courseLookupService = courseLookupService;
        _teeboxLookupService = teeboxLookupService;
        _holeLookupService = holeLookupService;
    }

    [HttpGet]
    [Route("scorecards/add")]
    public IActionResult AddRound()
    {
        var form = new CreateRoundFormModel();
        return View(form);
    }

    [HttpGet]
    [Route("/scorecards/course-search")]
    public async Task<IActionResult> SearchForCourseHtmx([FromQuery] string? course)
    {
        if (course is null)
        {
            return PartialView("Shared/_CreateRoundCourseSearchResults", new List<Course>());
        }
        
        var course_results = await _courseLookupService.CourseSearchByName(course);
        return PartialView("Shared/_CreateRoundCourseSearchResults", course_results);
    }
    
    [HttpGet]
    [Route("/scorecards/get-course-data/{courseId:long}")]
    public async Task<IActionResult> GetCourseData(long courseId)
    {
        // Retrieve the course. If it doesn't exist, set an error and redirect.
        var course = await _courseLookupService.GetCourseByIdAsync(courseId);
        if (course is null)
        {
            SetErrorMessage("Error occurred getting data for course.");
            return Redirect(nameof(AddRound));
        }

        // Retrieve the teeboxes dropdown list for the course.
        var teeboxes = await _teeboxLookupService.GetTeesDropdownForCourseAsync(courseId);

        // Create the view model with common properties.
        var viewModel = new CreateRoundFormModel
        {
            Course = course,
            TeeboxeSelectList = teeboxes
        };
        
        return PartialView("Shared/_RoundForm", viewModel);
    }
    
    
    [HttpGet]
    [Route("/scorecards/get-teebox-data/{teeboxId:long}")]
    public async Task<IActionResult> GetTeeboxData(long teeboxId)
    {
        // Retrieve the course. If it doesn't exist, set an error and redirect.
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(teeboxId);
        if (teebox is null)
        {
            SetErrorMessage("Error occurred getting data for teebox.");
            return Ok();
        }

        var holes = await _holeLookupService.GetHolesForTeeAsync(teeboxId);
        
        // Create the view model with common properties.
        var viewModel = new CreateRoundFormModel
        {
            Teebox = teebox,
            Holes = holes
        };
        
        return PartialView("Shared/_CreateRoundTeeboxData", viewModel);
    }

}