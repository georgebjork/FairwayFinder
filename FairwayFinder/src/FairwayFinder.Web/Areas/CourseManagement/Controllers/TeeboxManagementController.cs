using FairwayFinder.Core.Features.CourseManagement;
using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

public class TeeboxManagementController(IMediator mediator) : CourseManagementBaseController 
{
    [HttpGet]
    [Route("course/{courseId}/teebox/add")]
    public async Task<IActionResult> AddTeebox([FromRoute] int courseId)
    {
        return View(new TeeboxFormModel { CourseId = courseId });
    }
    
    [HttpPost]
    [Route("course/{courseId}/teebox/add")]
    public async Task<IActionResult> AddTeeboxPost([FromForm] TeeboxFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_TeeboxForm", form);
        }
        
        return PartialView("_TeeboxForm", form);    }
}
