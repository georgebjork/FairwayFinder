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

    [HttpGet]
    [Route("/profile/check-handle")]
    public async Task<IActionResult> CheckProfileHandle([FromQuery] string handle)
    {
        var form = new ProfileFormModel();
        form.Handle = handle;
        form.BaseUrl = RequestUrlBase;

        if (string.IsNullOrEmpty(handle))
        {
            form.IsValidHandle = false;
            return PartialView("_HandleValidationPartial", form);
        }
        
        var username = usernameRetriever.Username;
        var profile = await profileService.GetProfileByEmail(username);
        
        // It matches our profile! That is good and valid.
        if (profile!.Handle == handle)
        {
            form.IsValidHandle = true;
            return PartialView("_HandleValidationPartial", form);
        }
        
        // Check if it matches anyone else's
        var rv = await profileService.IsHandleAvailable(handle);
        
        form.IsValidHandle = rv;
        return PartialView("_HandleValidationPartial", form);
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

        form.UserId = usernameRetriever.UserId;
        form.BaseUrl = RequestUrlBase;
        
        var rv = await profileService.UpdateProfile(form);

        if (!rv)
        {
            SetErrorMessageHtmx("Something went wrong. Unable to update profile.");
            return PartialView("_EditProfileForm", form);
        }
        
        SetSuccessMessageHtmx("Profile successfully updated.");
        return PartialView("_EditProfileForm", form);
    }
}