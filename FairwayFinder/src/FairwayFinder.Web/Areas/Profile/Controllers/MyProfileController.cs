using FairwayFinder.Core.Features.Profile;
using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Features.Profile.Models.ViewModels;
using FairwayFinder.Core.Features.Profile.Services;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Profile.Controllers;

public class MyProfileController(IUsernameRetriever usernameRetriever, IMyProfileService myProfileService) : BaseProfileController
{
    [Route("/profile")]
    public async Task<IActionResult> EditProfile()
    {
        var username = usernameRetriever.Email;
        var profile = await myProfileService.GetProfileByEmail(username);

        if (profile is null)
        {
            return RedirectToAction(nameof(Index), "Home");
        }
        
        var vm = new EditProfileViewModel
        {
            Profile = profile,
            Form = new ProfileFormModel().ToForm(profile),
        };
        vm.Form.BaseUrl = RequestUrlBase;
        return View(vm);
    }

    [HttpGet]
    [Route("/profile/check-username")]
    public async Task<IActionResult> CheckProfileHandle([FromQuery] string userName)
    {
        var form = new ProfileFormModel
        {
            UserName = userName,
            BaseUrl = RequestUrlBase
        };

        if (string.IsNullOrEmpty(userName))
        {
            form.IsValidHandle = false;
            return PartialView("_HandleValidationPartial", form);
        }
        
        var email = usernameRetriever.Email;
        var profile = await myProfileService.GetProfileByEmail(email);
        
        // It matches our profile! That is good and valid.
        if (profile!.UserName == userName)
        {
            form.IsValidHandle = true;
            return PartialView("_HandleValidationPartial", form);
        }
        
        // Check if it matches anyone else's
        var rv = await myProfileService.IsHandleAvailable(userName);
        
        form.IsValidHandle = rv;

        if (form.IsValidHandle) form.UsernameValidationMessage = "Username is available.";
        else form.UsernameValidationMessage = "Username is not available.";
        
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
        
        var rv = await myProfileService.UpdateProfile(form);

        if (!rv)
        {
            SetErrorMessageHtmx("Something went wrong. Unable to update profile.");
            return PartialView("_EditProfileForm", form);
        }
        
        SetSuccessMessageHtmx("Profile successfully updated.");
        return PartialView("_EditProfileForm", form);
    }
}