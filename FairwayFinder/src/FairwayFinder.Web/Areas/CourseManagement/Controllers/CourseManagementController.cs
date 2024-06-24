using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

public class CourseManagementController(ICourseManagementService courseManagementService, IMediator mediator) : CourseManagementBaseController
{
    [HttpGet]
    [Route("course/add")]
    public async Task<IActionResult> AddCourse()
    {
        return View(new CourseFormModel());
    }
    
    [HttpPost]
    [Route("course/add")]
    public async Task<IActionResult> AddCoursePost([FromForm] CourseFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("Shared/_CourseFormModel", form);
        }

        var courseId = await courseManagementService.AddCourse(form);

        if (courseId <= 0)
        {
            SetErrorMessageHtmx("An error occured creating golf course.");
            return PartialView("Shared/_CourseFormModel", form);
        }
        
        SetSuccessMessage($"{form.Name} golf course has been added.");
        Response.Headers["HX-Redirect"] = Url.Action("ViewCourse", "Course", new { courseId });
        return Ok();
    }
}