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
    private readonly IProfileService _profileService;
    private readonly IUsernameRetriever _usernameRetriever;
    
    public EditProfileController(UserManager<ApplicationUser> userManager, ILogger<EditProfileController> logger, IProfileService profileService, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _profileService = profileService;
        _usernameRetriever = usernameRetriever;
    }

    [HttpGet]
    [Route("{userId}")]
    public async Task<IActionResult> EditProfile([FromRoute] string userId)
    {
        var profile = await _profileService.GetProfile(userId);
        var profile_documents = await _profileService.GetProfilePictureRecordsAsync(userId);

        if (profile is null)
        {
            _logger.LogError("User with is {user} came back null", userId);
            SetErrorMessage("User does not exist");

            SetHtmxRedirect("/");
            return RedirectToRoute("/");
        }

        var vm = new EditProfileViewModel
        {
            User = profile,
            Form = new EditProfileFormModel
            {
                FirstName = profile.FirstName ?? "",
                LastName = profile.LastName ?? "",
                Email = profile.Email ?? "",
                Username = profile.UserName ?? "",
                ProfilePictures = profile_documents
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

        var profile = await _profileService.UpdateProfile(userId, form);
        var profile_pictures = await _profileService.GetProfilePictureRecordsAsync(userId);

        form.ProfilePictures = profile_pictures;        
        if (!profile)
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
        
        var rv = await _profileService.CheckUsername(username);
        return PartialView("Shared/_UsernameField", new EditProfileFormModel
        {
            Username = username,
            IsValidUsername = rv
        });
    }

    [HttpDelete]
    [Route("delete-profile-picture/{documentId}")]
    public async Task<IActionResult> DeleteProfilePicture([FromRoute] string documentId)
    {
        await _profileService.DeleteProfilePictureAsync(documentId);
        return Ok();
    }
}