using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Features.Courses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Courses.Controllers;

public class CourseController(ICourseManagementService courseManagementService, IMediator mediator) : CoursesBaseController
{
    [HttpGet]
    [Route("courses")]
    public async Task<IActionResult> ViewAllCourses()
    {
        var result = await mediator.Send(new ViewCourseAllRequest());

        return result.Match<IActionResult>(
            View,
            err =>
            {
                SetErrorMessage($"{err.Message}");
                return RedirectToAction("Index", "Home");
            }
        );
    }
    
    [HttpGet]
    [Route("course/{courseId:int}")]
    public async Task<IActionResult> ViewCourse([FromRoute] int courseId)
    {
        var result = await mediator.Send(new ViewCourseRequest { CourseId = courseId});

        return result.Match<IActionResult>(
            View,
            err =>
            {
                SetErrorMessage($"{err.Message}");
                return RedirectToAction("ViewAllCourses");
            }
        );
    }
}