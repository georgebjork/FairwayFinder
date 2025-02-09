using FairwayFinder.Core.Features.UserManagement.Models.FormModels;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.UserManagement.Controllers;

public class EditUserController : BaseUserManagementController
{
    private readonly ILogger<EditUserController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUsernameRetriever _usernameRetriever;

    public EditUserController(ILogger<EditUserController> logger, UserManager<ApplicationUser> userManager, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _userManager = userManager;
        _usernameRetriever = usernameRetriever;
    }

    [Route("user-management/{userId}")]
    public async Task<IActionResult> EditUser([FromRoute] string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        // Dont want to edit a user that does not exist
        if (user is null)
        {
            SetErrorMessage("User was not found");
            return Redirect("Index", controllerName: "UserManagement");
        }

        return View(user);
    }
    
    [HttpGet]
    [Route("user-management/{userId}/roles/edit")]
    public async Task<IActionResult> EditUserRoles([FromRoute] string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        // Dont want to edit a user that does not exist
        if (user is null)
        {
            SetErrorMessage("User was not found");
            return Redirect("Index", controllerName: "UserManagement");
        }

        var current_roles = await _userManager.GetRolesAsync(user);
        var available_roles = Roles.GetAllRoles();
        available_roles = available_roles.Where(x => !current_roles.Contains(x)).ToList();

        var form = new UserRolesFormModel
        {
            UserId = user.Id,
            AvailableRoles = available_roles,
            CurrentRoles = current_roles.ToList()
        };

        return PartialView("_EditUserRolesForm", form);
    }
    
    
    [HttpPost]
    [Route("user-management/{userId}/roles/edit/assign")]
    public async Task<IActionResult> AssignUserRole([FromRoute] string userId, [FromForm] UserRolesFormModel form)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessageHtmx("An error occurred, please try again later.");
            return PartialView("_EditUserRolesForm", form);
        }
        
        var user = await _userManager.FindByIdAsync(userId);

        // Dont want to edit a user that does not exist
        if (user is null)
        {
            SetErrorMessageHtmx("The user you are updating was not found.");
            return PartialView("_EditUserRolesForm", form);
        }

        var valid_role =  Roles.GetAllRoles().Contains(form.SelectRole!);

        if (!valid_role)
        {
            SetErrorMessageHtmx("The role you selected was not a valid role.");
            return PartialView("_EditUserRolesForm", form);
        }
        
        var rv = await _userManager.AddToRoleAsync(user, form.SelectRole!);

        if (!rv.Succeeded)
        {
            SetErrorMessageHtmx("An error occurred, please try again later.");
            return PartialView("_EditUserRolesForm", form);
        }

        var current_roles = await _userManager.GetRolesAsync(user);
        var available_roles = Roles.GetAllRoles();
        available_roles = available_roles.Where(x => !current_roles.Contains(x)).ToList();

        form.CurrentRoles = current_roles.ToList();
        form.AvailableRoles = available_roles;
        
        return PartialView("_EditUserRolesForm", form);
    }
    
    
    [HttpPost]
    [Route("user-management/{userId}/roles/edit/remove")]
    public async Task<IActionResult> RemoveUserRole([FromRoute] string userId, [FromForm] UserRolesFormModel form)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessageHtmx("An error occurred, please try again later.");
            return PartialView("_EditUserRolesForm", form);
        }
        
        var user = await _userManager.FindByIdAsync(userId);

        // Dont want to edit a user that does not exist
        if (user is null)
        {
            SetErrorMessageHtmx("The user you are updating was not found.");
            return PartialView("_EditUserRolesForm", form);
        }

        var valid_role =  Roles.GetAllRoles().Contains(form.SelectRole!);

        if (!valid_role)
        {
            SetErrorMessageHtmx("The role you selected was not a valid role.");
            return PartialView("_EditUserRolesForm", form);
        }
        
        var rv = await _userManager.RemoveFromRoleAsync(user, form.SelectRole!);

        if (!rv.Succeeded)
        {
            SetErrorMessageHtmx("An error occurred, please try again later.");
            return PartialView("_EditUserRolesForm", form);
        }

        var current_roles = await _userManager.GetRolesAsync(user);
        var available_roles = Roles.GetAllRoles();
        available_roles = available_roles.Where(x => !current_roles.Contains(x)).ToList();

        form.CurrentRoles = current_roles.ToList();
        form.AvailableRoles = available_roles;
        
        return PartialView("_EditUserRolesForm", form);
    }
}