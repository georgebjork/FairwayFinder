using FairwayFinder.Core.Features.UserManagement.Models.FormModels;
using FairwayFinder.Core.Identity;
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
    [Route("change-password")]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordFormModel());
    }
    
    [HttpPost]
    [Route("change-password")]
    public async Task<IActionResult> ChangePasswordPost([FromForm] ChangePasswordFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_ChangePasswordForm", form);
        }
        
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            SetErrorMessage("Unable to find user.");
            return Redirect(nameof(ChangePassword));
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
        
        SetSuccessMessage("Password successfully changed");
        return Redirect(nameof(ChangePassword));
    }
}