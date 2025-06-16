using FairwayFinder.Core.Features.GolfCourse.Models;
using FairwayFinder.Core.Features.GolfCourse.Models.FormModels;
using FairwayFinder.Core.Features.GolfCourse.Models.ViewModels;
using FairwayFinder.Core.Features.GolfCourse.Services.Interfaces;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Course.Controllers;

[Route("course")]
public class CourseController : BaseCourseController
{
    private readonly ILogger<CourseController> _logger;
    private readonly ICourseService _courseService;
    private readonly ICourseStatsService _courseStatsService;
    private readonly ITeeboxService _teeboxService;
    private readonly IUsernameRetriever _usernameRetriever;

    public CourseController(ILogger<CourseController> logger, ICourseService courseService, ITeeboxService teeboxService, ICourseStatsService courseStatsService, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _courseService = courseService;
        _teeboxService = teeboxService;
        _courseStatsService = courseStatsService;
        _usernameRetriever = usernameRetriever;
    }

    [Route("all")]
    public async Task<IActionResult> Index()
    {
        var courses = await _courseService.GetAllCoursesAsync();
        var vm = new CourseManagementViewModel
        {
            Courses = courses
        };
        return View(vm);
    }
    
    [Route("{courseId:long}")]
    public async Task<IActionResult> ViewCourse([FromRoute]long courseId)
    {
        var course = await _courseService.GetCourseByIdAsync(courseId);
        var tees = await _teeboxService.GetTeesForCourseAsync(courseId);
        var course_stats = await _courseStatsService.GetAllCourseStatsAsync(new CourseStatsRequest { CourseId = courseId, UserId = _usernameRetriever.UserId });
        
        if (course == null)
        {
            SetErrorMessage("Course not found.");
            return RedirectToAction(nameof(Index));
        }

        course_stats.StatsRequest.CourseId = courseId;
        var vm = new CourseViewModel
        {
            Course = course,
            Teeboxes = tees,
            CourseStats = course_stats
        };
        
        return View(vm);
    }
    
    [HttpGet]
    [Route("add")]
    public IActionResult AddCourse()
    {
        return View();
    }
    
    [HttpPost]
    [Route("add")]
    public async Task<IActionResult> AddCoursePost([FromForm] CourseFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_CourseForm", form);
        }
        
        var rv = await _courseService.AddCourseAsync(form);

        if (rv < 0)
        {
            SetErrorMessageHtmx("An error occured while adding new course, please try again.");
            return PartialView("_CourseForm", form);
        }
        
        SetSuccessMessage("Course added successfully.");
        return RedirectToAction(nameof(Index));
    }
    
    [HttpGet]
    [Route("{courseId:long}/edit")]
    public async Task<IActionResult> EditCourse(long courseId)
    {
        var course = await _courseService.GetCourseByIdAsync(courseId);

        if (course is null)
        {
            SetErrorMessageHtmx("Course not found.");
            return RedirectToAction(nameof(Index));
        }

        var vm = course.ToFormModel();
        return View(vm);
    }
    
    [HttpPost]
    [Route("{courseId:long}/edit")]
    public async Task<IActionResult> EditCoursePost([FromRoute]long courseId, [FromForm] CourseFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_CourseForm", form);
        }
        
        var rv = await _courseService.UpdateCourseAsync(courseId, form);

        if (!rv)
        {
            SetErrorMessageHtmx("An error occured while updating course, please try again.");
            return PartialView("_CourseForm", form);
        }
        
        SetSuccessMessage("Course updated successfully.");
        return RedirectToAction(nameof(ViewCourse), new { courseId });
    }
}