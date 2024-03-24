using FairwayFinder.Core.Features.Profile.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Profile.Controllers;

public class ProfileController(IProfileService profileService) : BaseProfileController
{
    

    [Route("/profile/{handle}")]
    public async Task<IActionResult> ViewProfile([FromRoute] string handle)
    {
        var vm = await profileService.GetProfile(handle);

        if (vm is not null) return View(vm);
        
        SetErrorMessage("Profile does not exist");
        return NotFound();
    }
}