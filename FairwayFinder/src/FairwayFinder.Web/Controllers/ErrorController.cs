using System.Diagnostics;
using FairwayFinder.Web.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Controllers;

public class ErrorController : BaseController
{
    public IActionResult Error(int? statusCode = null)
    {
        if (statusCode.HasValue)
        {
            switch (statusCode)
            {
                case 401: return Redirect(nameof(NotAuthorized));
                case 404: return Redirect(nameof(NotFoundResponse));
                case 500: return Redirect(nameof(ServerError));
            }
        }

        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [Route("/401")]
    public IActionResult NotAuthorized()
    {
        return View();
    }
    
    [Route("/404")]
    public IActionResult NotFoundResponse() 
    {
        return View();
    }
    
    [Route("/500")]
    public IActionResult ServerError() 
    {
        return View();
    }
    
}