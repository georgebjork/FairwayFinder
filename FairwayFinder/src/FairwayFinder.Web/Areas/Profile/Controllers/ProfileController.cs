using Microsoft.AspNetCore.Mvc;


namespace FairwayFinder.Web.Areas.Profile.Controllers;

[Route("profile")]
public class ProfileController : BaseProfileController
{
    [HttpGet]
    [Route("")]
    public IActionResult ViewProfile()
    {
        return View();
    }
}