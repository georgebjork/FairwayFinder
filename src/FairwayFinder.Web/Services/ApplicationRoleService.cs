using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Web.Services;

public interface IApplicationRoleService
{
    Task EnsureRolesExistAsync();
}

public class ApplicationRoleService : IApplicationRoleService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ApplicationRoleService> _logger;

    public ApplicationRoleService(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger<ApplicationRoleService> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task EnsureRolesExistAsync()
    {
        var roles = new[] { ApplicationRoles.Admin, ApplicationRoles.User };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                _logger.LogInformation("Creating application role: {Role}", role);
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}