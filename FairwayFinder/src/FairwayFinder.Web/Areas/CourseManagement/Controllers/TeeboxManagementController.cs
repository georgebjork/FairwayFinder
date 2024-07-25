using FairwayFinder.Core.Features.CourseManagement;
using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

public class TeeboxManagementController(IMediator mediator) : CourseManagementBaseController {
    [HttpGet]
    [Route("course/{courseId}/teebox/add")]
    public async Task<IActionResult> AddTeebox([FromRoute] int courseId)
    {
        return View(new TeeboxFormModel
        {
            CourseId = courseId
        });
    }

    [HttpPost]
    [Route("course/{courseId}/teebox/add")]
    public async Task<IActionResult> AddTeebox([FromRoute] int courseId, [FromForm] TeeboxFormModel form)
    {
        if (!ModelState.IsValid){
            return PartialView("_TeeboxForm", form);
        }

        var result = await mediator.Send(new CreateTeeboxRequest { Form = form });

        return result.Match<IActionResult>(
            rv => {
                SetSuccessMessage($"{form.Name} golf course has been added.");

                var url = Url.Action("ViewCourse", "Course", new
                {
                    Area = "Courses",
                    courseId = rv
                });
                
                return Redirect(url);
            },
            err => {
                SetErrorMessageHtmx($"{err.Message}");
                return PartialView("_TeeboxForm", form);
            }
        );
    }
    
    
    [HttpGet]
    [Route("course/{courseId}/teebox/{teeboxId}/edit")]
    public async Task<IActionResult> EditTeebox([FromRoute] int courseId, [FromRoute] int teeboxId)
    {
        var result = await mediator.Send(new EditTeeboxRequest { TeeboxId = teeboxId });

        return result.Match<IActionResult>(
            rv => {
                return View(rv);
            },
            err => {
                SetErrorMessageHtmx($"{err.Message}");
                
                var url = Url.Action("ViewCourse", "Course", new { Area = "Courses", courseId});
                return Redirect(url);
            }
        );
    }
    
    
    [HttpPost]
    [Route("course/{courseId}/teebox/{teeboxId}/edit")]
    public async Task<IActionResult> UpdateTeebox([FromRoute] int courseId, [FromRoute] int teeboxId, [FromForm] TeeboxFormModel form)
    {
        var result = await mediator.Send(new UpdateTeeboxRequest { TeeboxId = teeboxId, Form = form });

        return result.Match<IActionResult>(
        rv => {
            SetSuccessMessageHtmx("Teebox has been successfully updated.");
            return PartialView("_TeeboxForm", form);
        },
        err => {
            SetErrorMessageHtmx($"{err.Message}");
                
            var url = Url.Action("EditTeebox", new { courseId, teeboxId });
            return Redirect(url);
        });
    }
    
    
    [HttpDelete]
    [Route("course/{courseId}/teebox/{teeboxId}/delete")]
    public async Task<IActionResult> DeleteTeebox([FromRoute] int courseId, [FromRoute]  int teeboxId)
    {
        var result = await mediator.Send(new DeleteTeeboxRequest { TeeboxId = teeboxId });

        var url = Url.Action("ViewCourse", "Course", new { Area = "Courses", courseId });
        return result.Match<IActionResult>(
        rv => {

            if (rv){
                SetSuccessMessage("Teebox has been successfully been deleted.");
                return Redirect(url);
            }
            
            SetErrorMessage("An error occurred, unable to delete teebox");
            return Redirect(url);
        },
        err =>
        {
            SetErrorMessage($"{err.Message}");
            return Redirect(url);
        });
    }
}
