using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services.Admin;

public class UserAdminService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IProfileService profileService)
{
    public async Task<List<UserAdminDto>> GetAllUsersAsync()
    {
        var users = userManager.Users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
        var profiles = await LoadProfilesAsync();
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
                Username = user.Handle,
                Roles = roles.ToList(),
                IsLockedOut = isLockedOut,
                IsEmailConfirmed = user.EmailConfirmed,
                IsSearchHidden = user.IsSearchHidden,
                PublicProfile = PublicProfileOf(profiles.GetValueOrDefault(user.Id)),
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
        var profiles = await LoadProfilesAsync(id);

        return new UserAdminDto
        {
            Id = user.Id,
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Username = user.Handle,
            Roles = roles.ToList(),
            IsLockedOut = isLockedOut,
            IsEmailConfirmed = user.EmailConfirmed,
            IsSearchHidden = user.IsSearchHidden,
            PublicProfile = PublicProfileOf(profiles.GetValueOrDefault(user.Id)),
            CreatedOn = user.CreatedOn
        };
    }

    public async Task<IdentityResult> UpdateUserAsync(string id, UpdateUserDto dto)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;

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

    public async Task<IdentityResult> SetSearchHiddenAsync(string userId, bool hidden)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        user.IsSearchHidden = hidden;

        return await userManager.UpdateAsync(user);
    }

    /// <summary>
    /// Sets <paramref name="targetUserId"/>'s public stats page settings. Same rules as the app's
    /// <c>PUT /api/profile/public</c>; <paramref name="actingAdminUserId"/> is only the audit stamp.
    /// </summary>
    public Task<PublicProfileSettingsResult> SetPublicProfileAsync(
        string targetUserId, PublicProfileSettingsRequest settings, string actingAdminUserId) =>
        profileService.UpdatePublicProfileAsync(targetUserId, settings, actingAdminUserId);

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

    private async Task<Dictionary<string, UserProfile>> LoadProfilesAsync(string? userId = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.UserProfiles
            .AsNoTracking()
            .Where(p => !p.IsDeleted && (userId == null || p.UserId == userId))
            .ToDictionaryAsync(p => p.UserId);
    }

    // A user who never opened their profile has no row yet: everything off, every section shown.
    private static PublicProfileSettingsRequest PublicProfileOf(UserProfile? profile) => profile is null
        ? new PublicProfileSettingsRequest()
        : new PublicProfileSettingsRequest
        {
            IsPublic = profile.IsPublic,
            Slug = profile.PublicSlug,
            ShowStrokesGained = profile.ShowStrokesGained,
            ShowRecentRounds = profile.ShowRecentRounds,
            ShowCourseNames = profile.ShowCourseNames
        };
}
