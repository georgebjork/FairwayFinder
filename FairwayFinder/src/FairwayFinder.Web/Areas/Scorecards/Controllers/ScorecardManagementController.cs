using System.Text.Json;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace FairwayFinder.Web.Areas.Scorecards.Controllers;

public class ScorecardManagementController : BaseScorecardController
{
    private readonly ILogger<ScorecardManagementController> _logger;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly CourseLookupService _courseLookupService;
    private readonly TeeboxLookupService _teeboxLookupService;
    private readonly HoleLookupService _holeLookupService;
    private readonly ScorecardService _scorecardService;
    private readonly ScorecardManagementService _scorecardManagementService;
    private readonly ILookupRepository _lookupRepository;

    private readonly IDistributedCache _cache;
    
    public ScorecardManagementController(ILogger<ScorecardManagementController> logger, IUsernameRetriever usernameRetriever, CourseLookupService courseLookupService, TeeboxLookupService teeboxLookupService, HoleLookupService holeLookupService, ScorecardService scorecardService, ILookupRepository lookupRepository, ScorecardManagementService scorecardManagementService, IDistributedCache cache)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _courseLookupService = courseLookupService;
        _teeboxLookupService = teeboxLookupService;
        _holeLookupService = holeLookupService;
        _scorecardService = scorecardService;
        _lookupRepository = lookupRepository;
        _scorecardManagementService = scorecardManagementService;
        _cache = cache;
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
    public async Task<IActionResult> GetCourseDataHtmx(long courseId)
    {
        var course = await _courseLookupService.GetCourseByIdAsync(courseId);
        var teebox_select = await _lookupRepository.GetTeesForCourseAsync(courseId);
        
        // Build form. Cache this. We want to reuse it across forms so less data retrival.
        var form = new ScorecardFormModel
        {
            RoundFormModel = new RoundFormModel
            {
                CourseId = course.course_id,
                CourseName = course.course_name
            },
            TeeboxSelectList = teebox_select
        };
        
        return PartialView("Shared/_RoundForm", form);
    }
    
    // Step 3: We've selected a teebox we need all of the data that comes with the teebox data.
    [HttpGet]
    [Route("/scorecards/get-teebox-data/{teeboxId:long}")]
    public async Task<IActionResult> GetTeeboxAndHoleDataHtmx(long teeboxId)
    {
        // Get our teebox data
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(teeboxId);
        
        // Get our course
        var course = await _courseLookupService.GetCourseByIdAsync(teebox!.course_id) ?? new Course();
        
        // Get our holes for the desired teebox and create a form model for each hole and store the hole in each form model
        var holes = await _holeLookupService.GetHolesForTeeAsync(teeboxId);
        var holeScoresForms = holes.Select(h => new HoleScoreFormModel { Par = h.par, Yardage = h.yardage, HoleNumber = h.hole_number, HoleId = h.hole_id}).ToList();

        var missTypes = await _lookupRepository.GetMissTypes();
        
        // Combine into one scorecard model
        var form = new ScorecardFormModel
        {
            Course = course,
            Teebox = teebox,
            HoleScore = holeScoresForms,
            MissTypeSelectList = missTypes
        };
        
        await _cache.SetStringAsync(_usernameRetriever.UserId, JsonSerializer.Serialize(form), 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            }
        );

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
        
        var result = await _scorecardManagementService.CreateNewScorecardAsync(form);

        if (result <= 0)
        {
            SetErrorMessageHtmx("Error occurred adding new round. Please try again.");
            form = await RefreshFormModelForError(form);
            return PartialView("Shared/_RoundForm", form);
        }

        await _cache.RemoveAsync(_usernameRetriever.UserId);
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
        
        await _cache.SetStringAsync(_usernameRetriever.UserId, JsonSerializer.Serialize(form), 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            }
        );

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
        
        var result = await _scorecardManagementService.UpdateScorecardAsync(form);

        if (!result)
        {
            SetErrorMessageHtmx("Error occurred updating round. Please try again.");
            form = await RefreshFormModelForError(form);
            form.IsUpdate = true;
            form.RoundFormModel.RoundId = roundId;
            return PartialView("Shared/_RoundForm", form);
        }
        
        await _cache.RemoveAsync(_usernameRetriever.UserId);
        SetSuccessMessage("Successfully updated round.");
        return Redirect("ViewScorecard", new { username = _usernameRetriever.Username, roundId }, "Scorecard");
    }
    
    private async Task<ScorecardFormModel> RefreshFormModelForError(ScorecardFormModel form)
    {
        var cached_form_json = await _cache.GetStringAsync(_usernameRetriever.UserId);
        
        if (cached_form_json is not null)
        {
            var cached_form = JsonSerializer.Deserialize<ScorecardFormModel>(cached_form_json);
            return cached_form ?? new ScorecardFormModel();
        }
        
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