using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Features.Profile.Models.ViewModels;
using FairwayFinder.Core.Features.Profile.Services;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;


namespace FairwayFinder.Web.Areas.Profile.Controllers;

[Route("profile")]
public class EditProfileController : BaseProfileController
{
    private readonly ILogger<EditProfileController> _logger;
    private readonly IProfileService _profile_service;
    private readonly IUsernameRetriever _usernameRetriever;
    
    public EditProfileController(UserManager<ApplicationUser> userManager, ILogger<EditProfileController> logger, IProfileService profileService, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _profile_service = profileService;
        _usernameRetriever = usernameRetriever;
    }

    [HttpGet]
    [Route("{userId}")]
    public async Task<IActionResult> EditProfile([FromRoute] string userId)
    {
        var user = await _profile_service.GetProfile(userId);

        if (user is null)
        {
            _logger.LogError("User with is {0} came back null", userId);
            SetErrorMessage("User does not exist");

            SetHtmxRedirect("/");
            return RedirectToRoute("/");
        }

        var vm = new EditProfileViewModel
        {
            User = user,
            Form = new EditProfileFormModel
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Email = user.Email ?? "",
                Username = user.UserName ?? ""
            }
        };
        
        if (IsHtmx()) return PartialView("EditProfile", vm);
        return View(vm);
    }
    
    [HttpPost]
    [Route("{userId}")]
    public async Task<IActionResult> EditProfilePost([FromRoute] string userId, [FromForm] EditProfileFormModel form)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("Shared/_EditProfileForm", form);
        }

        var rv = await _profile_service.UpdateProfile(userId, form);

        if (!rv)
        {
            SetErrorMessageHtmx("An error occurred updating user. Please try again.");
            return PartialView("Shared/_EditProfileForm", form);
        }

        form.IsValidUsername = null;
        SetSuccessMessageHtmx("Profile successfully updated.");
        return PartialView("Shared/_EditProfileForm", form);
    }
    
    [HttpGet]
    [Route("check-username")]
    public async Task<IActionResult> CheckUsername([FromQuery] string username)
    {
        if (string.IsNullOrEmpty(username) || _usernameRetriever.Username == username)
        {
            return PartialView("Shared/_UsernameField", new EditProfileFormModel
            {
                Username = username,
                IsValidUsername = null
            });
        }
        
        var rv = await _profile_service.CheckUsername(username);
        return PartialView("Shared/_UsernameField", new EditProfileFormModel
        {
            Username = username,
            IsValidUsername = rv
        });
    }
}