using FairwayFinder.Core.Features.CourseManagement;
using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

public class CourseManagementController(IMediator mediator) : CourseManagementBaseController
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
            return PartialView("_CourseForm", form);
        }
        
        var result = await mediator.Send(new CreateCourseRequest { Form = form });

        return result.Match<IActionResult>(
            courseId => {
                SetSuccessMessage($"{form.Name} golf course has been added.");
                Response.Headers["HX-Redirect"] = Url.Action("ViewCourse", "Course", new { Area = "Courses", courseId });
                return Ok();
            },
            err =>
            {
                SetErrorMessageHtmx($"{err.Message}");
                return PartialView("_CourseForm", form);
            }
        );
    }

    [HttpDelete]
    [Route("course/{courseId}/delete")]
    public async Task<IActionResult> DeleteCourse([FromRoute] int courseId)
    {
        var result = await mediator.Send(new DeleteCourseRequest { CourseId = courseId });

        return result.Match<IActionResult>(
            _ => {
                
                // This is from htmx so only the row needs to be removed
                if (CheckHtmxTrigger("delete-btn"))
                {
                    return Ok("");
                }
                
                SetSuccessMessage("Golf course has been successfully been deleted.");
                Response.Headers["HX-Redirect"] = Url.Action("Index", "Course", new { Area = "Courses" });
                return Ok();
            },
            err =>
            {
                SetErrorMessageHtmx($"{err.Message}");
                Response.Headers["HX-Redirect"] = Url.Action("Index", "Course", new { Area = "Courses" });
                return Ok();
            }
        );
    }
}