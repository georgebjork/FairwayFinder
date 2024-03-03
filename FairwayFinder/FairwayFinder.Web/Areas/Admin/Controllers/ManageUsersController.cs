using FairwayFinder.Core.Features.UserMangement;
using FairwayFinder.Core.Features.UserMangement.Models.FormModels;
using FairwayFinder.Core.Features.UserMangement.Models.ViewModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Settings;
using FairwayFinder.Web.Controllers.BaseControllers;
using FairwayFinder.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class ManageUsersController : BaseAuthorizedController {
    
    private readonly IManageUsersService manageUsersService;
    private readonly IUsernameRetriever usernameRetriever;
    private readonly UserManager<ApplicationUser> UserManager;
    
    public ManageUsersController(IManageUsersService manageUsersService, IUsernameRetriever usernameRetriever, UserManager<ApplicationUser> userManager) {
        this.manageUsersService = manageUsersService;
        this.usernameRetriever = usernameRetriever;
        UserManager = userManager;
    }

    [Route("/manage-users")]
    public async Task<IActionResult> ManageUsers() {

        var users = await manageUsersService.GetUsers();
        users.RemoveAll(u => u.Id == usernameRetriever.UserId);
        
        foreach (var user in users)
        {
            var rv = await UserManager.IsInRoleAsync(user, Roles.Admin);
            if (rv)
            {
                user.IsAdmin = true;
            }
        }

        var invites = await manageUsersService.GetInvites();
        var baseUrl = $"{Request.Scheme}://{Request.Host.Value}";
        foreach (var invite in invites)
        {
            invite.InviteUrl = $"{baseUrl}/register/{invite.invitation_identifier}";
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
        var invite = await manageUsersService.CreateInvite(form.Email);
        return RedirectToAction(nameof(ManageUsers));
    }
    
    
    [Route("/update-user-role/{userId}")]
    [HttpPost]
    public async Task<IActionResult> UpdateUserRole(string userId)
    {
        var user = await UserManager.FindByIdAsync(userId);
        
        // If admin then revoke
        if (await UserManager.IsInRoleAsync(user!, Roles.Admin))
        {
            var regUser = await manageUsersService.RevokeAdmin(userId);
            return PartialView("Shared/_UserTableRecord", regUser);
        }
        
        var adminUser = await manageUsersService.PromoteAdmin(userId);
        return PartialView("Shared/_UserTableRecord", adminUser);
    }
    
    [Route("/remove-user/{userId}")]
    [HttpPost]
    public async Task<IActionResult> RemoveUser(string userId)
    {
        var rv = await manageUsersService.RemoveUser(userId);
        return Content("");
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
