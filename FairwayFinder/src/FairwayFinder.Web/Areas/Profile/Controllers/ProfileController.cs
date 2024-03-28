using FairwayFinder.Core.Features.Profile.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Profile.Controllers;

public class ProfileController(IProfileService profileService) : BaseProfileController
{
    

    [Route("/profile/{userName}")]
    public async Task<IActionResult> ViewProfile([FromRoute] string userName)
    {
        var vm = await profileService.GetProfile(userName);

        if (vm is not null) return View(vm);
        
        SetErrorMessage("Profile does not exist");
        return NotFound();
    }
}