using FairwayFinder.Core.Features.Profile;
using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Features.Profile.Models.ViewModels;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Profile.Controllers;

public class ProfileController(IUsernameRetriever usernameRetriever, IProfileService profileService) : BaseProfileController
{
    [Route("/profile")]
    public async Task<IActionResult> Index()
    {
        var username = usernameRetriever.Username;
        var profile = await profileService.GetProfileByEmail(username);

        if (profile is null)
        {
            return RedirectToAction(nameof(Index), "Home");
        }
        
        var vm = new ProfileViewModel
        {
            Profile = profile,
            Form = new ProfileFormModel().ToForm(profile),
        };
        vm.Form.BaseUrl = RequestUrlBase;
        
        return View(vm);
    }

    [HttpPost]
    [Route("/profile")]
    public async Task<IActionResult> UpdateProfile([FromForm] ProfileFormModel form)
    {
        if (!ModelState.IsValid)
        {
            form.BaseUrl = RequestUrlBase;
            return PartialView("_EditProfileForm", form);
           
        }
        
        return RedirectToAction(nameof(Index));
    }
}