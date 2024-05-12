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
    
    
    /*[HttpPost]
    [Route("course/add")]
    public IActionResult AddCourse()
    {
        return View();
    } */
}