using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Models.ViewModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

public class CourseManagementController : BaseCourseManagementController
{
    private readonly ILogger<CourseManagementController> _logger;
    private readonly CourseManagementService _courseManagementService;

    public CourseManagementController(ILogger<CourseManagementController> logger, CourseManagementService courseManagementService)
    {
        _logger = logger;
        _courseManagementService = courseManagementService;
    }

    [Route("course-management")]
    public async Task<IActionResult> Index()
    {
        var courses = await _courseManagementService.GetAllCoursesAsync();

        var vm = new CourseManagementViewModel
        {
            Courses = courses
        };
        return View(vm);
    }
    
    [Route("course-management/{courseId:long}")]
    public async Task<IActionResult> ViewCourse([FromRoute]long courseId)
    {
        var course = await _courseManagementService.GetCourseByIdAsync(courseId);
        var tees = await _courseManagementService.GetTeeForCourseAsync(courseId);
        
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
    [Route("course-management/add")]
    public IActionResult AddCourse()
    {
        return View();
    }
    
    [HttpPost]
    [Route("course-management/add")]
    public async Task<IActionResult> AddCoursePost([FromForm] CourseFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_CourseForm", form);
        }
        
        var rv = await _courseManagementService.AddCourseAsync(form);

        if (rv < 0)
        {
            SetErrorMessageHtmx("An error occured while adding new course, please try again.");
            return PartialView("_CourseForm", form);
        }
        
        SetSuccessMessage("Course added successfully.");
        return Redirect(nameof(Index));
    }
    
    [HttpGet]
    [Route("course-management/{courseId:long}/edit")]
    public async Task<IActionResult> EditCourse(long courseId)
    {
        var course = await _courseManagementService.GetCourseByIdAsync(courseId);

        if (course is null)
        {
            SetErrorMessageHtmx("Course not found.");
            return Redirect(nameof(Index));
        }

        var vm = course.ToFormModel();
        return View(vm);
    }
    
    [HttpPost]
    [Route("course-management/{courseId:long}/edit")]
    public async Task<IActionResult> EditCoursePost([FromRoute]long courseId, [FromForm] CourseFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_CourseForm", form);
        }
        
        var rv = await _courseManagementService.UpdateCourseAsync(courseId, form);

        if (!rv)
        {
            SetErrorMessageHtmx("An error occured while updating course, please try again.");
            return PartialView("_CourseForm", form);
        }
        
        SetSuccessMessage("Course updated successfully.");
        return Redirect(nameof(ViewCourse), new { courseId });
    }
    
    
    [HttpGet]
    [Route("course-management/{courseId:long}/teebox/add")]
    public async Task<IActionResult> AddTee([FromRoute] long courseId)
    {
        var form = new TeeboxFormModel
        {
            CourseId = courseId
        };
        return View(form);
    }
    
    
    [HttpPost]
    [Route("course-management/{courseId:long}/teebox/add")]
    public async Task<IActionResult> AddTeePost([FromRoute] long courseId, [FromForm] TeeboxFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_TeeboxForm", form);
        }

        var rv = await _courseManagementService.AddTeeAsync(courseId, form);

        if (rv <= 0)
        {
            SetErrorMessageHtmx("An error occured adding Tee Box, please try again.");
            return PartialView("_TeeboxForm");
        }
        
        SetSuccessMessage("Tee Box successfully added.");
        return Redirect(nameof(ViewCourse), new {courseId});
    }
}