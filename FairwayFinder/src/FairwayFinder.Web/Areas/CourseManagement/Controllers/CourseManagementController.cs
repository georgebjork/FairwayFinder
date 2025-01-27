using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Models.ViewModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
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

        if (course == null)
        {
            SetErrorMessage("Course not found.");
            return RedirectToAction(nameof(Index));
        }
        
        var vm = new CourseViewModel
        {
            Course = course
        };
        
        return View(vm);
    }
    

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
        
        var rv = await _courseManagementService.AddCourse(form);

        if (rv < 0)
        {
            SetErrorMessageHtmx("An error occured while adding new course, please try again.");
            return PartialView("_CourseForm", form);
        }
        
        SetSuccessMessage("Course added successfully.");
        return Redirect(nameof(Index));
    }
    
    [Route("course-management/{courseId:long}/edit")]
    public async Task<IActionResult> EditCourse(long courseId)
    {
        var course = await _courseManagementService.GetCourseByIdAsync(courseId);

        if (course is null)
        {
            SetErrorMessageHtmx("Course not found.");
            return Redirect(nameof(Index));
        }

        var vm = new CourseFormModel
        {
            course_id = course.course_id,
            name = course.course_name,
            address = course.address,
            phone_number = course.phone_number
        };
        
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
        
        var rv = await _courseManagementService.UpdateCourse(courseId, form);

        if (!rv)
        {
            SetErrorMessageHtmx("An error occured while updating course, please try again.");
            return PartialView("_CourseForm", form);
        }
        
        SetSuccessMessage("Course updated successfully.");
        return Redirect(nameof(ViewCourse), new { courseId });
    }
}