using FairwayFinder.Core.Features.Admin.UserManagement;
using FairwayFinder.Core.Features.Admin.UserManagement.Models.FormModels;
using FairwayFinder.Core.Features.Admin.UserManagement.Models.ViewModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Authorization;
using FairwayFinder.Core.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Admin.Controllers;


public class ManageUsersController : BaseAdminController {
    
    private readonly IManageUsersService _manageUsersService;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRoleRefreshService _roleRefreshService;
    
    public ManageUsersController(IManageUsersService manageUsersService, IUsernameRetriever usernameRetriever, UserManager<ApplicationUser> userManager, IRoleRefreshService roleRefreshService) {
        _manageUsersService = manageUsersService;
        _usernameRetriever = usernameRetriever;
        _userManager = userManager;
        _roleRefreshService = roleRefreshService;
    }

    [Route("/manage-users")]
    public async Task<IActionResult> ManageUsers() {

        var users = await _manageUsersService.GetUsers();
        users.RemoveAll(u => u.Id == _usernameRetriever.UserId);
        
        foreach (var user in users)
        {
            var rv = await _userManager.IsInRoleAsync(user, Roles.Admin);
            if (rv)
            {
                user.IsAdmin = true;
            }
        }

        var invites = await _manageUsersService.GetInvites();
        foreach (var invite in invites)
        {
            invite.InviteUrl = $"{RequestUrlBase}/register/{invite.invitation_identifier}";
        }

        var vm = new ManageUserViewModel {
            Users = users,
            Invites = invites
        };
        
        return View(vm);
    }


    [Route("invite-user")]
    public async Task<IActionResult> InviteUser([FromForm(Name = "InviteFormModel")] UserInviteFormModel form)
    {
        var invite = await _manageUsersService.CreateAndSendInvite(form.Email, $"{Request.Scheme}://{Request.Host.Value}");
        return RedirectToAction(nameof(ManageUsers));
    }
    
    
    [Route("/update-user-role/{userId}")]
    [HttpPost]
    public async Task<IActionResult> UpdateUserRole(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        // If admin then revoke
        if (await _userManager.IsInRoleAsync(user!, Roles.Admin))
        {
            var regUser = await _manageUsersService.RevokeAdmin(userId);
            await _roleRefreshService.SetRefreshFlag(userId);
            
            return PartialView("Shared/_UserTableRecord", regUser);
        }
        
        var adminUser = await _manageUsersService.PromoteAdmin(userId);
        await _roleRefreshService.SetRefreshFlag(userId);
        
        return PartialView("Shared/_UserTableRecord", adminUser);
    }
    
    [Route("/remove-user/{userId}")]
    [HttpPost]
    public async Task<IActionResult> RemoveUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        var utcDate = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rv = await _userManager.SetLockoutEndDateAsync(user!, new DateTimeOffset(utcDate));
        
        await _roleRefreshService.SetRefreshFlag(userId);
        return PartialView("Shared/_UserTableRecord", user);
    }
    
    [Route("/enable-user/{userId}")]
    [HttpPost]
    public async Task<IActionResult> ReEnableUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        var rv = await _userManager.SetLockoutEndDateAsync(user!, null);
        return PartialView("Shared/_UserTableRecord", user);
    }
    
    [Route("/revoke-invite/{inviteId}")]
    [HttpPost]
    public async Task<IActionResult> RevokeInvite(string inviteId)
    {
        var rv = await _manageUsersService.RevokeInvite(inviteId);
        
        if(!rv) RedirectToAction(nameof(ManageUsers));
        
        return Content("");
    }
}
