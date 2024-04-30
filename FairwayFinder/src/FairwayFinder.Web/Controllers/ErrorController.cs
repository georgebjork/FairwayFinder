using FairwayFinder.Web.Controllers.BaseControllers;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Controllers;

public class ErrorController : BaseController
{
    [Route("/401")]
    public IActionResult NotAuthorized()
    {
        Response.Headers["HX-Redirect"] = Url.Action(nameof(NotAuthorized));
        return View();
    }

    [Route("/404")]
    public IActionResult NotFoundResponse()
    {
        Response.Headers["HX-Redirect"] = Url.Action(nameof(NotFoundResponse));
        return View();
    }
    
    [Route("/500")]
    public IActionResult ServerError()
    {
        Response.Headers["HX-Redirect"] = Url.Action(nameof(ServerError));
        return View();
    }
}