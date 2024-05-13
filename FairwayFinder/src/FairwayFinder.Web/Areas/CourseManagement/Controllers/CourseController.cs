using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

public class CourseController(ILogger<CourseController> logger, ICourseManagementService courseManagementService) : CourseBaseController
{
    [HttpGet]
    [Route("courses")]
    public async Task<IActionResult> Index()
    {
        var vm = await courseManagementService.GetAllCourses();
        return View(vm);
    }
    
    [HttpGet]
    [Route("course/{courseId:int}")]
    public async Task<IActionResult> ViewCourse([FromRoute] int courseId)
    {
        var vm = await courseManagementService.GetCourseById(courseId);

        if (vm.Course is not null) return View(vm);
        
        logger.LogWarning("Course with id {0} was not found.", courseId);
        
        SetErrorMessage("Course does not exist or was deleted.");
        return RedirectToAction(nameof(Index));
    }
    
    
    [HttpGet]
    [Route("course/add")]
    public async Task<IActionResult> AddCourse()
    {
        return View(new EditCourseFormModel());
    }
    
    [HttpPost]
    [Route("course/add")]
    public async Task<IActionResult> AddCoursePost([FromForm] EditCourseFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("Shared/_CourseFormModel", form);
        }

        var courseId = await courseManagementService.AddCourse(form);

        if (courseId <= 0)
        {
            SetErrorMessageHtmx("An error ocurred creating golf course.");
            return PartialView("Shared/_CourseFormModel", form);
        }
        
        SetSuccessMessage($"{form.Name} golf course has been added.");
        Response.Headers["HX-Redirect"] = Url.Action(nameof(ViewCourse), new { courseId });
        return Ok();
    }
}