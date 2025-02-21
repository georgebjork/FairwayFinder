using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
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
    private readonly ILookupRepository _lookupRepository;

    public ScorecardManagementController(ILogger<ScorecardManagementController> logger, IUsernameRetriever usernameRetriever, CourseLookupService courseLookupService, TeeboxLookupService teeboxLookupService, HoleLookupService holeLookupService, ScorecardService scorecardService, ILookupRepository lookupRepository)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _courseLookupService = courseLookupService;
        _teeboxLookupService = teeboxLookupService;
        _holeLookupService = holeLookupService;
        _scorecardService = scorecardService;
        _lookupRepository = lookupRepository;
    }

    [HttpGet]
    [Route("scorecards/add")]
    public IActionResult AddRound()
    {
        var form = new ScorecardFormModel();
        return View(form);
    }

    // Step 1: find the golf course
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
    
    // Step 2: With out selected golf course, get our course data needed to create the round
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

        var form = new ScorecardFormModel
        {
            Course = course,
            TeeboxSelectList = await _lookupRepository.GetTeesForCourseAsync(course.course_id)
        };
        
        return PartialView("Shared/_RoundForm", form);
    }
    
    // Step 3: We've selected a teebox we need all of the data that comes with the teebox data.
    // 1. 
    [HttpGet]
    [Route("/scorecards/get-teebox-data/{teeboxId:long}")]
    public async Task<IActionResult> GetTeeboxData(long teeboxId)
    {
        // Get our teebox data
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(teeboxId);
        if (teebox == null)
        {
            SetErrorMessage("Error occurred getting data for teebox.");
            return Ok();
        }
        
        // Get our holes and create a form model for each hole and store the hole in each form model
        var holes = await _holeLookupService.GetHolesForTeeAsync(teebox.teebox_id);
        var holeScores = holes.Select(h => new HoleScoreFormModel { Par = h.par, Yardage = h.yardage, HoleNumber = h.hole_number, HoleId = h.hole_id}).ToList();
        
        // Get our course
        var course = await _courseLookupService.GetCourseByIdAsync(teebox.course_id) ?? new Course();

        // Combine into one scorecard model
        var form = new ScorecardFormModel
        {
            Teebox = teebox,
            HoleScore = holeScores,
            MissTypeSelectList = await _lookupRepository.GetMissTypes(),
            Course = course
        };

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
        // Retrieve the round first
        var round = await _scorecardService.GetScorecardByIdAsync(roundId);
        if (round == null)
        {
            SetErrorMessage("Round does not exist.");
            return RedirectToAction(nameof(Index), "Scorecard", new { username = _usernameRetriever.Username });
        }

        // Start independent tasks concurrently
        var courseTask = _courseLookupService.GetCourseByIdAsync(round.course_id);
        var teeboxTask = _teeboxLookupService.GetTeeByIdAsync(round.teebox_id);
        var teeboxesDropdownTask = _lookupRepository.GetTeesForCourseAsync(round.course_id);
        var missTypesTask = _lookupRepository.GetMissTypes();
        var holeScoresTask = _scorecardService.GetHoleScoreFormsByRoundIdAsync(roundId);
        var holeStatsTask = _scorecardService.GetHoleScoreStatsFormsByRoundIdAsync(roundId);

        await Task.WhenAll(courseTask, teeboxTask, teeboxesDropdownTask, missTypesTask, holeScoresTask, holeStatsTask);

        var course = courseTask.Result;
        if (course == null)
        {
            SetErrorMessage("Golf course does not exist.");
            return RedirectToAction(nameof(Index), "Scorecard", new { username = _usernameRetriever.Username });
        }

        var teebox = teeboxTask.Result;
        var teeboxesDropdown = teeboxesDropdownTask.Result;
        var missTypes = missTypesTask.Result;
        var holeScores = holeScoresTask.Result;
        var holeStats = holeStatsTask.Result;

        // Build a dictionary for quick lookup of hole stats by HoleId.
        var holeStatsLookup = holeStats.ToDictionary(stat => stat.HoleId);

        // Assign corresponding HoleStats to each HoleScore.
        foreach (var score in holeScores)
        {
            if (holeStatsLookup.TryGetValue(score.HoleId, out var stats))
            {
                score.HoleStats = stats;
            }
        }

        // Create the RoundFormModel with the relevant data.
        var roundFormModel = new RoundFormModel
        {
            RoundId = round.round_id,
            DatePlayed = round.date_played,
            UsingHoleStats = round.using_hole_stats,
            CourseId = course.course_id,
            CourseName = course.course_name,
            TeeboxId = teebox.teebox_id
        };

        // Build the overall form model.
        var form = new ScorecardFormModel
        {
            IsUpdate = true,
            RoundFormModel = roundFormModel,
            Course = course,
            Teebox = teebox,
            TeeboxSelectList = teeboxesDropdown,
            MissTypeSelectList = missTypes,
            HoleScore = holeScores
        };

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
            form.RoundFormModel.RoundId = roundId;
            return PartialView("Shared/_RoundForm", form);
        }
        
        var result = await _scorecardService.UpdateScorecardAsync(form);

        if (!result)
        {
            SetErrorMessageHtmx("Error occurred updating round. Please try again.");
            form = await RefreshFormModelForError(form);
            form.IsUpdate = true;
            form.RoundFormModel.RoundId = roundId;
            return PartialView("Shared/_RoundForm", form);
        }
        
        SetSuccessMessage("Successfully updated round.");
        return Redirect("ViewScorecard", new { username = _usernameRetriever.Username, roundId }, "Scorecard");
    }
    
    private async Task<ScorecardFormModel> RefreshFormModelForError(ScorecardFormModel form)
    {
        var course = await _courseLookupService.GetCourseByIdAsync(form.RoundFormModel.CourseId);
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(form.RoundFormModel.TeeboxId);

        if (course is null || teebox is null) return form; // Should not happen, but just in case
        
        var teeboxes_dropdown = await _lookupRepository.GetTeesForCourseAsync(course.course_id);
        var miss_dropdowns = await _lookupRepository.GetMissTypes();
        
        form.Course = course;
        form.Teebox = teebox;
        form.TeeboxSelectList = teeboxes_dropdown;
        form.MissTypeSelectList = miss_dropdowns;

        return form;
    }


}