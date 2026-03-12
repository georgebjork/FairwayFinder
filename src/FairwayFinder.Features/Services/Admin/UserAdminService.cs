using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Features.Services.Admin;

public class UserAdminService(UserManager<ApplicationUser> userManager)
{
    public async Task<List<UserAdminDto>> GetAllUsersAsync()
    {
        var users = userManager.Users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
        var result = new List<UserAdminDto>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var isLockedOut = await userManager.IsLockedOutAsync(user);

            result.Add(new UserAdminDto
            {
                Id = user.Id,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList(),
                IsLockedOut = isLockedOut,
                IsEmailConfirmed = user.EmailConfirmed,
                CreatedOn = user.CreatedOn
            });
        }

        return result;
    }

    public async Task<UserAdminDto?> GetUserByIdAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return null;

        var roles = await userManager.GetRolesAsync(user);
        var isLockedOut = await userManager.IsLockedOutAsync(user);

        return new UserAdminDto
        {
            Id = user.Id,
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToList(),
            IsLockedOut = isLockedOut,
            IsEmailConfirmed = user.EmailConfirmed,
            CreatedOn = user.CreatedOn
        };
    }

    public async Task<IdentityResult> UpdateUserAsync(string id, UpdateUserDto dto)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.UpdatedOn = DateTime.UtcNow;

        return await userManager.UpdateAsync(user);
    }

    public async Task<IdentityResult> DeleteUserAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        return await userManager.DeleteAsync(user);
    }

    public async Task<IdentityResult> ToggleAdminRoleAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");

        return isAdmin
            ? await userManager.RemoveFromRoleAsync(user, "Admin")
            : await userManager.AddToRoleAsync(user, "Admin");
    }

    public async Task<IdentityResult> SetLockoutAsync(string userId, bool lockout)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        if (lockout)
        {
            await userManager.SetLockoutEnabledAsync(user, true);
            return await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }
        else
        {
            return await userManager.SetLockoutEndDateAsync(user, null);
        }
    }
}
