using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Scorecards.Controllers;

public class ScorecardManagementController : BaseScorecardController
{
    private readonly ILogger<ScorecardManagementController> _logger;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly CourseLookupService _courseLookupService;
    private readonly TeeboxLookupService _teeboxLookupService;
    private readonly HoleLookupService _holeLookupService;
    private readonly ScorecardService _scorecardService;

    public ScorecardManagementController(ILogger<ScorecardManagementController> logger, IUsernameRetriever usernameRetriever, CourseLookupService courseLookupService, TeeboxLookupService teeboxLookupService, HoleLookupService holeLookupService, ScorecardService scorecardService)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _courseLookupService = courseLookupService;
        _teeboxLookupService = teeboxLookupService;
        _holeLookupService = holeLookupService;
        _scorecardService = scorecardService;
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

        var form = await BuildCourseFormModelData(course);
        return PartialView("Shared/_RoundForm", form);
    }
    
    
    [HttpGet]
    [Route("/scorecards/get-teebox-data/{teeboxId:long}")]
    public async Task<IActionResult> GetTeeboxData(long teeboxId)
    {
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(teeboxId);
        if (teebox is null)
        {
            SetErrorMessage("Error occurred getting data for teebox.");
            return Ok();
        }

        var form = await BuildTeeboxFormModelData(teebox); 
        return PartialView("Shared/_CreateRoundTeeboxData", form);
    }

    [HttpPost]
    [Route("/scorecards/add")]
    public async Task<IActionResult> AddRoundPost([FromForm] ScorecardFormModel form)
    {
        if (!ModelState.IsValid)
        {
            form = await RefreshFormModelForError(form);
            return PartialView("Shared/_RoundForm", form);
        }
        
        var result = await _scorecardService.CreateNewScorecardAsync(form);

        if (result <= 0)
        {
            SetErrorMessageHtmx("Error occurred adding new round. Please try again.");
            form = await RefreshFormModelForError(form);
            return PartialView("Shared/_RoundForm", form);
        }
        
        SetSuccessMessage("Successfully added round.");
        return Redirect("ViewScorecard", new { username = _usernameRetriever.Username, roundId = result }, "Scorecard");
    }
    
    [HttpGet]
    [Route("scorecards/{roundId:long}/edit")]
    public async Task<IActionResult> EditRound([FromRoute] long roundId)
    {
        var form = new ScorecardFormModel();
        var round = await _scorecardService.GetScorecardByIdAsync(roundId);

        if (round is null)
        {
            SetErrorMessage("Round does not exist.");
            return Redirect(nameof(Index), controllerName: "Scorecard", routeValues: new {username = _usernameRetriever.Username});
        }
        var course = await _courseLookupService.GetCourseByIdAsync(round.course_id);
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(round.teebox_id);
        var teeboxes_dropdown = await _teeboxLookupService.GetTeesDropdownForCourseAsync(round.course_id);
        
        if (course is null)
        {
            SetErrorMessage("Golf course does not exist.");
            return Redirect(nameof(Index), controllerName: "Scorecard", routeValues: new {username = _usernameRetriever.Username});
        }

        var hole_scores = await _scorecardService.GetHoleScoreFormsByRoundIdAsync(roundId);

        form.IsUpdate = true;
        form.RoundId = round.round_id;
        form.DatePlayed = round.date_played;
        
        form.CourseId = course.course_id;
        form.CourseName = course.course_name;
        form.Course = course;
        
        form.TeeboxId = teebox.teebox_id.ToString();
        form.TeeboxSelectList = teeboxes_dropdown;
        form.Teebox = teebox;
        
        form.HoleScore = hole_scores;
        
        return View(form);
    }


    [HttpPost]
    [Route("scorecards/{roundId:long}/edit")]
    public async Task<IActionResult> EditRoundPost([FromRoute] long roundId, [FromForm] ScorecardFormModel form)
    {
        if (!ModelState.IsValid)
        {
            form = await RefreshFormModelForError(form);
            form.IsUpdate = true;
            form.RoundId = roundId;
            return PartialView("Shared/_RoundForm", form);
        }
        
        var result = await _scorecardService.UpdateScorecardAsync(form);

        if (!result)
        {
            SetErrorMessageHtmx("Error occurred updating round. Please try again.");
            form = await RefreshFormModelForError(form);
            form.IsUpdate = true;
            form.RoundId = roundId;
            return PartialView("Shared/_RoundForm", form);
        }
        
        SetSuccessMessage("Successfully updated round.");
        return Redirect("ViewScorecard", new { username = _usernameRetriever.Username, roundId }, "Scorecard");
    }
    
    
    
    
    
    private async Task<ScorecardFormModel> BuildCourseFormModelData(Course course)
    {
        var vm = new ScorecardFormModel();
        
        var teeboxes_dropdown = await _teeboxLookupService.GetTeesDropdownForCourseAsync(course.course_id);
        vm.TeeboxSelectList = teeboxes_dropdown;
        vm.Course = course;
        
        return vm;
    }
    
    private async Task<ScorecardFormModel> BuildTeeboxFormModelData(Teebox teebox)
    {
        var vm = new ScorecardFormModel();
        var holes = await _holeLookupService.GetHolesForTeeAsync(teebox.teebox_id);
        var hole_scores = new List<HoleScoreFormModel>();

        foreach (var hole in holes)
        {
            var hs = new HoleScoreFormModel
            {
                HoleId = hole.hole_id,
                Par = hole.par,
                Yardage = hole.yardage,
                HoleNumber = hole.hole_number
            };
            hole_scores.Add(hs);
        }

        vm.Teebox = teebox;
        vm.HoleScore = hole_scores;
        
        var course = await _courseLookupService.GetCourseByIdAsync(teebox.course_id);
        if (course is null) // This should never happen but just to make the complier happy.
        {
            _logger.LogError("Course with id {0} came back null when trying to retrive form data.", teebox.course_id);
            return vm;
        }

        vm.Course = course;

        return vm;
    }
    
    private async Task<ScorecardFormModel> RefreshFormModelForError(ScorecardFormModel form)
    {
        var course = await _courseLookupService.GetCourseByIdAsync(form.CourseId);
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(int.Parse(form.TeeboxId));

        if (course is null || teebox is null) return form; // Should not happen, but just in case
        
        var teeboxes_dropdown = await _teeboxLookupService.GetTeesDropdownForCourseAsync(course.course_id);
        var holes = await _holeLookupService.GetHolesForTeeAsync(teebox.teebox_id);
        
        form.Course = course;
        form.Teebox = teebox;
        form.TeeboxSelectList = teeboxes_dropdown;

        return form;
    }


}