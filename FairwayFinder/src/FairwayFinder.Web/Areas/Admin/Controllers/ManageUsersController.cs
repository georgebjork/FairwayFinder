using FairwayFinder.Core.Features.Admin.UserManagement;
using FairwayFinder.Core.Features.Admin.UserManagement.Models.FormModels;
using FairwayFinder.Core.Features.Admin.UserManagement.Models.ViewModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Settings;
using FairwayFinder.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Admin.Controllers;


public class ManageUsersController(
    IManageUsersService manageUsersService,
    IUsernameRetriever usernameRetriever,
    UserManager<ApplicationUser> userManager,
    IUserRefreshService userRefreshService)
    : BaseAdminController
{
    [Route("/manage-users")]
    public async Task<IActionResult> ManageUsers() {

        var users = await manageUsersService.GetUsers();
        users.RemoveAll(u => u.Id == usernameRetriever.UserId);
        
        foreach (var user in users)
        {
            var rv = await userManager.IsInRoleAsync(user, Roles.Admin);
            if (rv)
            {
                user.IsAdmin = true;
            }
        }

        var invites = await manageUsersService.GetInvites();
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
        var invite = await manageUsersService.CreateAndSendInvite(form.Email, $"{Request.Scheme}://{Request.Host.Value}");
        return RedirectToAction(nameof(ManageUsers));
    }
    
    
    [Route("/update-user-role/{userId}")]
    [HttpPost]
    public async Task<IActionResult> UpdateUserRole(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        
        // If admin then revoke
        if (await userManager.IsInRoleAsync(user!, Roles.Admin))
        {
            var regUser = await manageUsersService.RevokeAdmin(userId);
            await userRefreshService.SetRefreshFlag(userId);
            
            return PartialView("Shared/_UserTableRecord", regUser);
        }
        
        var adminUser = await manageUsersService.PromoteAdmin(userId);
        await userRefreshService.SetRefreshFlag(userId);
        
        return PartialView("Shared/_UserTableRecord", adminUser);
    }
    
    [Route("/remove-user/{userId}")]
    [HttpPost]
    public async Task<IActionResult> RemoveUser(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        
        var utcDate = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rv = await userManager.SetLockoutEndDateAsync(user!, new DateTimeOffset(utcDate));
        
        await userRefreshService.SetRefreshFlag(userId);
        return PartialView("Shared/_UserTableRecord", user);
    }
    
    [Route("/enable-user/{userId}")]
    [HttpPost]
    public async Task<IActionResult> ReEnableUser(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        
        var rv = await userManager.SetLockoutEndDateAsync(user!, null);
        return PartialView("Shared/_UserTableRecord", user);
    }
    
    [Route("/revoke-invite/{inviteId}")]
    [HttpPost]
    public async Task<IActionResult> RevokeInvite(string inviteId)
    {
        var rv = await manageUsersService.RevokeInvite(inviteId);
        
        if(!rv) RedirectToAction(nameof(ManageUsers));
        
        return Content("");
    }
}
