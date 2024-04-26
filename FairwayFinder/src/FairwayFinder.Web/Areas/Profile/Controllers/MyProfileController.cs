using FairwayFinder.Core.Features.Profile;
using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Features.Profile.Models.ViewModels;
using FairwayFinder.Core.Features.Profile.Services;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Profile.Controllers;

public class MyProfileController(
    IUsernameRetriever usernameRetriever, 
    IMyProfileService myProfileService, 
    UserManager<ApplicationUser> userManager, 
    SignInManager<ApplicationUser> signInManager) : BaseProfileController
{
    [Route("profile")]
    public async Task<IActionResult> Index()
    {
        var username = usernameRetriever.Email;
        var profile = await myProfileService.GetProfileByEmail(username);

        if (profile is null)
        {
            return RedirectToAction(nameof(Index), "Home");
        }
        
        var vm = new EditProfileViewModel { Profile = profile, };

        return View();
    }
    
    
    [Route("/profile/edit")]
    public async Task<IActionResult> EditProfile()
    {
        var username = usernameRetriever.Email;
        var profile = await myProfileService.GetProfileByEmail(username);
        
        if (profile is null)
        {
            // Set the HX-Redirect header to the URL where you want to redirect
            Response.Headers["HX-Redirect"] = Url.Action(nameof(Index), "Home");
            return new EmptyResult();  // Return an empty result with the header set
        }
        
        var vm = new EditProfileViewModel
        {
            Profile = profile,
            Form = new ProfileFormModel().ToForm(profile),
        };
        
        vm.Form.BaseUrl = RequestUrlBase;
        return PartialView("_EditProfileForm", vm.Form);
    }

    [HttpPost]
    [Route("/profile/update")]
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
    
    [Route("/profile/change-password")]
    public async Task<IActionResult> EditPassword()
    {
        return PartialView("_ChangePasswordForm", new ChangePasswordFormModel());
    }
    
    [HttpPost]
    [Route("/profile/change-password")]
    public async Task<IActionResult> EditPassword([FromForm] ChangePasswordFormModel form)
    {
        if (!ModelState.IsValid) return PartialView("_ChangePasswordForm", form);

        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        var changePasswordResult = await userManager.ChangePasswordAsync(user, form.OldPassword, form.NewPassword);
        if (!changePasswordResult.Succeeded)
        {
            foreach (var error in changePasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return PartialView("_ChangePasswordForm", form);
        }

        await signInManager.RefreshSignInAsync(user);
        
        SetSuccessMessageHtmx("Password successfully changed");
        return PartialView("_ChangePasswordForm", new ChangePasswordFormModel());
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
}


/*
 * public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("User changed their password successfully.");
            StatusMessage = "Your password has been changed.";

            return RedirectToPage();
        }
 */