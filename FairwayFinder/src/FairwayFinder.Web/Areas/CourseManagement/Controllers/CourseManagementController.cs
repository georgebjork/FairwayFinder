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
    public async Task<IActionResult> AddCourse([FromForm] CourseFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_CourseForm", form);
        }
        
        var result = await mediator.Send(new CreateCourseRequest { Form = form });

        return result.Match<IActionResult>(
            courseId => {
                SetSuccessMessage($"{form.Name} golf course has been added.");

                var url = Url.Action("ViewCourse", "Course", new { Area = "Courses", courseId });
                return Redirect(url);
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

        var url = Url.Action("ViewAllCourses", "Course", new { Area = "Courses" });
        return result.Match<IActionResult>(
            _ => {
                
                // This is from htmx so only the row needs to be removed
                if (CheckHtmxTrigger("delete-btn"))
                {
                    return Ok();
                }
                
                SetSuccessMessage("Golf course has been successfully been deleted.");
                return Redirect(url);
            },
            err =>
            {
                SetErrorMessage($"{err.Message}");
                return Redirect(url);
            }
        );
    }

    [HttpGet]
    [Route("course/{courseId}/edit")]
    public async Task<IActionResult> EditCourse([FromRoute] int courseId)
    {
        var result = await mediator.Send(new EditCourseRequest { CourseId = courseId });
        
        return result.Match<IActionResult>(
            View,
            err =>
            {
                SetErrorMessage($"{err.Message}");

                var url = Url.Action("ViewAllCourses", "Course", new { Area = "Courses" });
                return Redirect(url);
            }
        );
    }
    
    [HttpPost]
    [Route("course/{courseId}/edit")]
    public async Task<IActionResult> UpdateCourse([FromRoute] int courseId, [FromForm] CourseFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_CourseForm", form);
        }
        
        var result = await mediator.Send(new UpdateCourseRequest { CourseId = courseId, Form = form });

        return result.Match<IActionResult>(
            rv =>
            {
                if (rv)
                {
                    SetSuccessMessage("Course successfully updated");
                    return Redirect(Url.Action("EditCourse", new { courseId }));
                }
                
                SetErrorMessage("Course was unable to be updated");
                return Redirect(Url.Action("EditCourse", new { courseId }));
            },
            err =>
            {
                SetErrorMessage($"{err.Message}");
                return Redirect(Url.Action("ViewAllCourses", "Course", new { Area = "Courses" }));
            }
        );
    }
}