using FairwayFinder.Core.Features.GolfCourse.Models.FormModels;
using FairwayFinder.Core.Features.GolfCourse.Models.ViewModels;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Course.Controllers;

[Route("course")]
public class CourseController : BaseCourseController
{
    private readonly ILogger<CourseController> _logger;
    private readonly ICourseService _courseService;
    private readonly ITeeboxService _teeboxService;
    private readonly IHoleService _holeService;

    public CourseController(ILogger<CourseController> logger, ICourseService courseService, ITeeboxService teeboxService, IHoleService holeService)
    {
        _logger = logger;
        _courseService = courseService;
        _teeboxService = teeboxService;
        _holeService = holeService;
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
        
        if (course == null)
        {
            SetErrorMessage("Course not found.");
            return RedirectToAction(nameof(Index));
        }
        
        var vm = new CourseViewModel
        {
            Course = course,
            Teeboxes = tees
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