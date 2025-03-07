using System.Text.Json;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Helpers;
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
        var scorecard = await _scorecardService.GetScorecardAsync(roundId);

        if (!scorecard.Success)
        {
            SetErrorMessage("An error occured retrieving scorecard.");
            return RedirectToAction(nameof(Index), "Scorecard", new { username = _usernameRetriever.Username });
        }
        
        // Start independent tasks concurrently
        var teebox_dropdown = await _lookupRepository.GetTeesForCourseAsync(scorecard.Course.course_id);
        var miss_types_dropdown = await _lookupRepository.GetMissTypes();


        var hole_score_form_list = new List<HoleScoreFormModel>();
        foreach (var hs in scorecard.ScoresList)
        {
            var hs_form = hs.ToForm();

            var hole_stat = scorecard.HoleStatsList.FirstOrDefault(x => hs.score_id == x.score_id);
            
            hs_form.HoleStats = hole_stat?.ToForm() ?? new HoleStatsFormModel();
            hole_score_form_list.Add(hs_form);
        }

        // Create the RoundFormModel with the relevant data.
        var round_form_model = scorecard.Round.ToForm();
        round_form_model.CourseName = scorecard.Course.course_name;

        // Build the overall form model.
        var form = new ScorecardFormModel
        {
            IsUpdate = true,
            RoundFormModel = round_form_model,
            Course = scorecard.Course,
            Teebox = scorecard.Teebox,
            TeeboxSelectList = teebox_dropdown,
            MissTypeSelectList = miss_types_dropdown,
            HoleScore = hole_score_form_list
        };
        
        // Cache this result. No need to get it all again.
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
            var cached_form = JsonSerializer.Deserialize<ScorecardFormModel>(cached_form_json) ?? new ScorecardFormModel();
            cached_form.HoleScore = form.HoleScore;
            return cached_form;
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