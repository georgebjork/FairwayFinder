using FairwayFinder.Core.Features.UserManagement.Models.FormModels;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Identity.Settings;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.UserManagement.Models.ViewModels;
using FairwayFinder.Core.UserManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.UserManagement.Controllers;

public class UserManagementController : BaseUserManagementController
{

    private readonly IUserManagementService _userManagementService;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserManagementController> _logger;

    public UserManagementController(IUserManagementService user_management_service, IUsernameRetriever username_retriever, UserManager<ApplicationUser> user_manager, ILogger<UserManagementController> logger)
    {
        _userManagementService = user_management_service;
        _usernameRetriever = username_retriever;
        _userManager = user_manager;
        _logger = logger;
    }

    [Route("manage-users")]
    public async Task<IActionResult> Index()
    {
        // These are users with a confirmed email
        var users = await _userManagementService.GetAllUsers();
        
        // Remove ourselves from the output.
        users.RemoveAll(u => u.Id == _usernameRetriever.UserId);
        foreach (var user in users)
        {
            user.IsAdmin = await _userManager.IsInRoleAsync(user, Roles.Admin);
        }
        
        //var pending_users = await _userManagementService.GetAllPendingUsers();
        var invited_users = await _userManagementService.GetAllInvitedUsers();
        foreach (var invite in invited_users)
        {
            invite.InviteUrl = $"{RequestUrlBase}/register/{invite.invitation_identifier}";
        }
        
        var vm = new ManageUsersViewModel {
            Users = users,
            Invites = invited_users
        };
        
        
        return View(vm);
    }

    [Route("manage-users/approve-email")]
    [HttpPost]
    public async Task<IActionResult> ApproveUserEmail([FromQuery] string userId)
    {
        var user = await _userManagementService.ApproveUserEmail(userId);

        if (user is null)
        {
            // This should never happen.
            _logger.LogError("No user was returned with Id: {0} in the ApproveUserEmail controller.", userId);
            return PartialView("_ApplicationUserTableRow", null);
        }
        
        _logger.LogInformation("Email {0} was manually confirmed by user {1}", user.Email, _usernameRetriever.Username);
        return PartialView("_ApplicationUserTableRow", user);
    }
    
    
    [Route("manage-users/disable")]
    [HttpPost]
    public async Task<IActionResult> DisableUser([FromQuery] string userId)
    {
        var user = await _userManagementService.DisableUser(userId);

        if (user is null)
        {
            // This should never happen.
            _logger.LogError("No user was returned with Id: {0} in the DisableUser controller.", userId);
            return PartialView("_ApplicationUserTableRow", null);
        }
        
        _logger.LogInformation("User {0} was manually disabled by user {1}", user.Email, _usernameRetriever.Username);
        return PartialView("_ApplicationUserTableRow", user);
    }
    
    
    [Route("manage-users/enable")]
    [HttpPost]
    public async Task<IActionResult> EnableUser([FromQuery] string userId)
    {
        var user = await _userManagementService.EnableUser(userId);

        if (user is null)
        {
            // This should never happen.
            _logger.LogError("No user was returned with Id: {0} in the Enable controller.", userId);
            return PartialView("_ApplicationUserTableRow", null);
        }
        
        _logger.LogInformation("User {0} was manually enabled by user {1}", user.Email, _usernameRetriever.Username);
        return PartialView("_ApplicationUserTableRow", user);
    }
    
    
    [Route("manage-users/invite-user")]
    [HttpPost]
    public async Task<IActionResult> InviteUser([FromForm][Bind(Prefix = "InviteFormModel")]InviteUserFormModel formModel)
    {
        if (ModelState.IsValid)
        {
            var rv = await _userManagementService.InviteUser(formModel.Email);

            if (rv)
            {
                SetSuccessMessage($"Invited {formModel.Email}");
                return RedirectToAction(nameof(Index));
            }
        }
        
        SetErrorMessage($"An error occurred adding user with email: {formModel.Email}");
        return RedirectToAction(nameof(Index));
    }
}