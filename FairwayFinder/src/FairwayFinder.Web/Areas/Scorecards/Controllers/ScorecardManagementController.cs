using System.Text.Json;
using FairwayFinder.Core.Features.Scorecards.Cache;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Identity.Settings;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace FairwayFinder.Web.Areas.Scorecards.Controllers;

public class ScorecardManagementController : BaseScorecardController
{
    private readonly ILogger<ScorecardManagementController> _logger;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly ICourseService _courseService;
    private readonly ITeeboxService _teeboxService;
    private readonly IHoleService _holeService;
    private readonly IScorecardService _scorecardService;
    private readonly ILookupRepository _lookupRepository;
    private readonly IAuthorizationService _authorizationService;

    private readonly IDistributedCache _cache;
    
    public ScorecardManagementController(ILogger<ScorecardManagementController> logger, IUsernameRetriever usernameRetriever, ICourseService courseService, ITeeboxService teeboxService, IHoleService holeService, IScorecardService scorecardService, ILookupRepository lookupRepository, IDistributedCache cache, IAuthorizationService authorizationService)
    {
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _courseService = courseService;
        _teeboxService = teeboxService;
        _holeService = holeService;
        _scorecardService = scorecardService;
        _lookupRepository = lookupRepository;
        _cache = cache;
        _authorizationService = authorizationService;
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
            return PartialView("Shared/_CreateRoundCourseSearchResults", new List<Core.Models.Course>());
        }
        
        var course_results = await _courseService.CourseSearchByName(course);
        return PartialView("Shared/_CreateRoundCourseSearchResults", course_results);
    }
    
    // Step 2: With out selected golf course, get our course data needed to create the round
    [HttpGet]
    [Route("/scorecards/get-course-data/{courseId:long}")]
    public async Task<IActionResult> GetCourseDataHtmx(long courseId)
    {
        var course = await _courseService.GetCourseByIdAsync(courseId);
        var teebox_select = await _lookupRepository.GetTeesForCourseAsync(courseId);

        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }
        
        // Build form. Cache this. We want to reuse it across forms so fewer data retrieval.
        var form = new ScorecardFormModel
        {
            Course = course,
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
    public async Task<IActionResult> GetTeeboxAndHoleDataHtmx(long teeboxId, bool fullRound, bool frontNine, bool backNine)
    {
        // Get our teebox data
        var teebox = await _teeboxService.GetTeeByIdAsync(teeboxId);
        
        // Get our course
        var course = await _courseService.GetCourseByIdAsync(teebox!.course_id) ?? new Core.Models.Course();
        
        // Get our holes for the desired teebox and create a form model for each hole and store the hole in each form model
        var holes = await _holeService.GetHolesForTeeAsync(teeboxId, frontNine, backNine);
        var holeScoresForms = holes.Select(h => new HoleScoreFormModel { Par = h.par, Yardage = h.yardage, HoleNumber = h.hole_number, HoleId = h.hole_id}).ToList();

        var missTypes = await _lookupRepository.GetMissTypes();
        
        var form = new ScorecardFormModel
        {
            Course = course,
            Teebox = teebox,
            HoleScore = holeScoresForms,
            MissTypeSelectList = missTypes,
            FullRound = (!frontNine && !backNine),
            FrontNine = frontNine,
            BackNine = backNine
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

        await _cache.RemoveAsync(_usernameRetriever.UserId);
        SetSuccessMessage("Successfully added round.");
        return Redirect("ViewScorecard", new { username = _usernameRetriever.Username, roundId = result }, "Scorecard");
    }
    
    [HttpGet]
    [Route("scorecards/{roundId:long}/edit")]
    public async Task<IActionResult> EditRound([FromRoute] long roundId)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, roundId, Policy.CanEditScorecard);
        if (!authResult.Succeeded)
        {
            _logger.LogError($"{_usernameRetriever.Email} tried to edit a scorecard they dont have access to.");
            return Unauthorized();
        }
        
        var scorecard = await _scorecardService.GetScorecardByRoundIdAsync(roundId);

        if (!scorecard.Success)
        {
            SetErrorMessage("An error occured retrieving scorecard.");
            return RedirectToAction(nameof(Index), "Scorecard", new { username = _usernameRetriever.Username });
        }
        
        // Start independent tasks concurrently
        var teebox_dropdown = await _lookupRepository.GetTeesForCourseAsync(scorecard.Course.course_id);
        var miss_types_dropdown = await _lookupRepository.GetMissTypes();


        var hole_score_form_list = new List<HoleScoreFormModel>();
        foreach (var hs in scorecard.HoleScoresList)
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
            HoleScore = hole_score_form_list,
            FullRound = scorecard.Round.full_round,
            BackNine = scorecard.Round.back_nine,
            FrontNine = scorecard.Round.front_nine
        };
        
        // Cache the form, no need to do this all again on reload or failure.
        await CacheForm(_usernameRetriever.UserId, roundId, form);
        return View(form);
    }


    [HttpPost]
    [Route("scorecards/{roundId:long}/edit")]
    public async Task<IActionResult> EditRoundPost([FromRoute] long roundId, [FromForm] ScorecardFormModel form)
    {
        if (!ModelState.IsValid)
        {
            var updated_form = await RefreshFormModelForError(form);
            updated_form.IsUpdate = true;
            updated_form.RoundFormModel.RoundId = roundId;
            updated_form.RoundFormModel.UsingHoleStats = form.RoundFormModel.UsingHoleStats;
            return PartialView("Shared/_RoundForm", updated_form);
        }
        
        var result = await _scorecardService.UpdateScorecardAsync(form);

        if (!result)
        {
            SetErrorMessageHtmx("Error occurred updating round. Please try again.");
            var updated_form = await RefreshFormModelForError(form);
            updated_form.IsUpdate = true;
            updated_form.RoundFormModel.RoundId = roundId;
            updated_form.RoundFormModel.UsingHoleStats = form.RoundFormModel.UsingHoleStats;
            return PartialView("Shared/_RoundForm", form);
        }
        
        // Clear the cache
        await _cache.RemoveAsync(ScorecardCacheKeys.GetScorecardFormCacheKey(_usernameRetriever.UserId, roundId));
        
        SetSuccessMessage("Successfully updated round.");
        return Redirect("ViewScorecard", new { username = _usernameRetriever.Username, roundId }, "Scorecard");
    }
    
    private async Task<ScorecardFormModel> RefreshFormModelForError(ScorecardFormModel form)
    {
        // Use the cached form
        var cached_form = await GetCachedForm(_usernameRetriever.UserId, form.RoundFormModel.RoundId ?? 0);
        if (cached_form is not null)
        {
            cached_form.HoleScore = form.HoleScore;
            return cached_form;
        }
        
        // Manually get the parts we need.
        var course = await _courseService.GetCourseByIdAsync(form.RoundFormModel.CourseId);
        var teebox = await _teeboxService.GetTeeByIdAsync(form.RoundFormModel.TeeboxId);

        if (course is null || teebox is null) return form; // Should not happen, but just in case
        
        var teeboxes_dropdown = await _lookupRepository.GetTeesForCourseAsync(course.course_id);
        var miss_dropdowns = await _lookupRepository.GetMissTypes();
        
        form.Course = course;
        form.Teebox = teebox;
        form.TeeboxSelectList = teeboxes_dropdown;
        form.MissTypeSelectList = miss_dropdowns;

        return form;
    }


    [HttpPost]
    [Route("scorecards/{roundId:long}/edit/include")]
    public async Task<IActionResult> UpdateRoundExclusion([FromRoute] long roundId, [FromQuery] bool exclude)
    {
        var result = await _scorecardService.UpdateRoundExclusion(roundId, exclude);

        if (!result) return BadRequest(); // This failed for whatever reason

        return PartialView("~/Areas/Scorecards/Views/Scorecard/Shared/_ExcludeFromStatsButton.cshtml", new Round
        {
            round_id = roundId, 
            exclude_from_stats = exclude
        });
    }

    private async Task CacheForm(string userId, long roundId, ScorecardFormModel form)
    {
        await _cache.SetStringAsync(
            ScorecardCacheKeys.GetScorecardFormCacheKey(userId, roundId), 
            JsonSerializer.Serialize(form), 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            }
        );
    }
    
    private async Task<ScorecardFormModel?> GetCachedForm(string userId, long roundId)
    {
        var form_string = await _cache.GetStringAsync(ScorecardCacheKeys.GetScorecardFormCacheKey(userId, roundId));
        
        // If the string is null, return null. Otherwise, attempt to deserialize
        return form_string is null ? null : JsonSerializer.Deserialize<ScorecardFormModel>(form_string);
    }
    
}