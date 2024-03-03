using FairwayFinder.Web.Controllers.BaseControllers;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Controllers;

public class ErrorController : BaseController
{
    [Route("/401")]
    public IActionResult NotAuthorized()
    {
        return View();
    }

    [Route("/404")]
    public IActionResult NotFound()
    {
        return View();
    }
    
    [Route("/500")]
    public IActionResult ServerError()
    {
        return View();
    }
}