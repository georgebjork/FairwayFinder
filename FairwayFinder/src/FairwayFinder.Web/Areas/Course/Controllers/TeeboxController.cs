using FairwayFinder.Core.Features.GolfCourse.Models;
using FairwayFinder.Core.Features.GolfCourse.Models.FormModels;
using FairwayFinder.Core.Features.GolfCourse.Models.ViewModels;
using FairwayFinder.Core.Features.GolfCourse.Services.Interfaces;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;


namespace FairwayFinder.Web.Areas.Course.Controllers;

[Route("course/{courseId}/teebox")]
public class TeeboxController : BaseCourseController
{
    private readonly ILogger<CourseController> _logger;
    private readonly ICourseService _courseService;
    private readonly ITeeboxService _teeboxService;
    private readonly IHoleService _holeService;
    private readonly ICourseStatsService _courseStatsService;
    private readonly IUsernameRetriever _usernameRetriever;

    public TeeboxController(ILogger<CourseController> logger, ICourseService courseService, ITeeboxService teeboxService, IHoleService holeService, ICourseStatsService courseStatsService, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _courseService = courseService;
        _teeboxService = teeboxService;
        _holeService = holeService;
        _courseStatsService = courseStatsService;
        _usernameRetriever = usernameRetriever;
    }
    
    [HttpGet]
    [Route("{teeboxId:long}")]
    public async Task<IActionResult> ViewTeebox([FromRoute] long courseId, [FromRoute] long teeboxId)
    {
        var tee = await _teeboxService.GetTeeByIdAsync(teeboxId);
        
        if (tee is null)
        {
            _logger.LogWarning($"Course teebox with id {teeboxId} not found.");
            SetErrorMessageHtmx("Course teebox not found.");
            return RedirectToAction("ViewCourse", "Course", new {courseId});
        }
        
        var course = await _courseService.GetCourseByIdAsync(courseId);
        var holes = await _holeService.GetHolesForTeeAsync(tee.teebox_id);
        var teebox_stats = await _courseStatsService.GetAllCourseStatsAsync(new CourseStatsRequest
        {
            CourseId = courseId,
            TeeboxId = teeboxId,
            UserId = _usernameRetriever.UserId
        });
        
        if (course == null)
        {
            SetErrorMessageHtmx("Course does not exist.");
            return RedirectToAction(nameof(Index), "Course");
        }
        
        teebox_stats.StatsRequest.CourseId = courseId;
        teebox_stats.StatsRequest.TeeboxId = teeboxId;
        var vm = new TeeboxViewModel
        {
            Course = course,
            Teebox = tee,
            Holes = holes,
            TeeboxStats = teebox_stats
        };
        return View(vm);
    }
    
    
    [HttpGet]
    [Route("teebox/add")]
    public async Task<IActionResult> AddTee([FromRoute] long courseId)
    {
        // We are going to check if any teeboxes exist for this course. If so, we can reuse the par and handicap, only yardages will change.
        var teeboxes = await _teeboxService.GetTeesForCourseAsync(courseId);

        var form = new TeeboxFormModel { CourseId = courseId };
        
        if (teeboxes.Count == 0)
        {
            form.Holes = Enumerable.Range(1, 18).Select(i => new HoleFormModel { HoleNumber = i }).ToList();
            return View(form);
        }

        form.Par = teeboxes.First().par;
        var holes = await _holeService.GetHolesForTeeAsync(teeboxes.First().teebox_id); // Doesnt matter which one, we will just take the first teebox
        
        foreach (var hole in holes)
        {
            form.Holes.Add(new HoleFormModel
            {
                HoleNumber = hole.hole_number,
                CourseId = courseId,
                Handicap = hole.handicap,
                Par = hole.par,
                ParHandicapReadonly = true
            });
        }

        form.Holes = form.Holes.OrderBy(h => h.HoleNumber).ToList(); // Order by hole number
        return View(form);
    }
    
    
    [HttpPost]
    [Route("add")]
    public async Task<IActionResult> AddTeePost([FromRoute] long courseId, [FromForm] TeeboxFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_TeeboxForm", form);
        }

        if (form.Yardage != form.Holes.Sum(h => h.Yardage))
        {
            ModelState.AddModelError(string.Empty, "Sum of yardages dont match Tee Box yardages.");
            return PartialView("_TeeboxForm", form);
        }

        var rv = await _teeboxService.AddTeeAsync(courseId, form);

        if (rv <= 0)
        {
            SetErrorMessageHtmx("An error occured adding Tee Box, please try again.");
            return PartialView("_TeeboxForm");
        }
        
        SetSuccessMessage("Tee Box successfully added.");
        return RedirectToAction("ViewCourse", "Course", new {courseId});
    }
    
    [HttpGet]
    [Route("{teeboxId:long}/edit")]
    public async Task<IActionResult> EditTee([FromRoute] long courseId, [FromRoute] long teeboxId)
    {
        var tee = await _teeboxService.GetTeeByIdAsync(teeboxId);

        if (tee is null)
        {
            SetErrorMessage("Tee Box not found.");
            return RedirectToAction(nameof(ViewTeebox), new { courseId, teeboxId });
        }

        var expected_holes_count = tee.is_nine_hole ? 9 : 18;
        var holes = await _holeService.GetHolesForTeeAsync(teeboxId);
        List<HoleFormModel> holes_form;
        
        if (holes.Count == expected_holes_count)
        {
            holes_form = holes.Select(hole => hole.ToFormModel()).ToList();
        }
        else
        {
            holes_form = Enumerable.Range(1, expected_holes_count)
                .Select(i => new HoleFormModel { HoleNumber = i })
                .ToList();

        }
        var vm = tee.ToFormModel();
        vm.Holes = holes_form;

        return View(vm);
    }
    
    [HttpPost]
    [Route("{teeboxId:long}/edit")]
    public async Task<IActionResult> EditTeePost([FromRoute] long courseId, [FromRoute] long teeboxId, [FromForm] TeeboxFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_TeeboxForm", form);
        }
        
        var rv = await _teeboxService.UpdateTeeAsync(teeboxId, form);

        if (!rv)
        {
            SetErrorMessageHtmx("An error occured while updating teebox, please try again.");
            return PartialView("_TeeboxForm", form);
        }
        
        SetSuccessMessage("Tee Box updated successfully.");
        return RedirectToAction(nameof(ViewTeebox), new { courseId, teeboxId });
    }
}