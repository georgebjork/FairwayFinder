using FairwayFinder.Core.Features.UserManagement.Models.FormModels;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Services;
using FairwayFinder.Web.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.UserManagement.Controllers;

public class ChangePasswordController : BaseUserManagementController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChangePasswordController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    [Route("user-management/{userId}/change-password")]
    public IActionResult ChangePassword([FromRoute] string userId)
    {
        return PartialView(new ChangePasswordFormModel
        {
            UserId = userId
        });
    }
    
    [HttpPost]
    [Route("user-management/{userId}/change-password")]
    public async Task<IActionResult> ChangePasswordPost([FromRoute] string userId, [FromForm] ChangePasswordFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_ChangePasswordForm", form);
        }
        
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            SetErrorMessageHtmx("Unable to find user.");
            return PartialView("_ChangePasswordForm", new ChangePasswordFormModel());
        }

        var result = await _userManager.ChangePasswordAsync(user, form.OldPassword, form.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return PartialView("_ChangePasswordForm", form);
        }

        await _signInManager.RefreshSignInAsync(user);
        
        SetSuccessMessageHtmx("Password successfully changed");
        return PartialView("_ChangePasswordForm", new ChangePasswordFormModel());
    }
}