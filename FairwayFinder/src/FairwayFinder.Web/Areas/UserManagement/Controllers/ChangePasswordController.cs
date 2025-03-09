using FairwayFinder.Core.Features.UserManagement.Models.FormModels;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Identity.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.UserManagement.Controllers;

public class ChangePasswordController : BaseUserManagementController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthorizationService _authorizationService;

    public ChangePasswordController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IAuthorizationService authorizationService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    [Route("user-management/{userId}/change-password")]
    public async Task<IActionResult> ChangePassword([FromRoute] string userId)
    {
        var auth_result = await _authorizationService.AuthorizeAsync(User, userId, Policy.CanEditProfile);
        if (!auth_result.Succeeded)
        {
            return new ForbidResult();
        }
        
        return PartialView(new ChangePasswordFormModel
        {
            UserId = userId
        });
    }
    
    [HttpPost]
    [Route("user-management/{userId}/change-password")]
    public async Task<IActionResult> ChangePasswordPost([FromRoute] string userId, [FromForm] ChangePasswordFormModel form)
    {
        var auth_result = await _authorizationService.AuthorizeAsync(User, userId, Policy.CanEditProfile);
        if (!auth_result.Succeeded)
        {
            return new ForbidResult();
        }
        
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