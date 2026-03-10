using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class ProfileService : IProfileService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        UserManager<ApplicationUser> userManager)
    {
        _dbContextFactory = dbContextFactory;
        _userManager = userManager;
    }

    public async Task<UserProfileResponse> GetOrCreateProfileAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

        if (profile is null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            profile = new UserProfile
            {
                UserId = userId,
                PublicIdentifier = Guid.NewGuid(),
                IsPublic = false,
                CreatedBy = userId,
                CreatedOn = today,
                UpdatedBy = userId,
                UpdatedOn = today,
                IsDeleted = false
            };

            dbContext.UserProfiles.Add(profile);
            await dbContext.SaveChangesAsync();
        }

        var displayName = await GetDisplayNameAsync(userId);

        return new UserProfileResponse
        {
            UserProfileId = profile.UserProfileId,
            UserId = profile.UserId,
            PublicIdentifier = profile.PublicIdentifier,
            IsPublic = profile.IsPublic,
            DisplayName = displayName
        };
    }

    public async Task<UserProfileResponse?> GetPublicProfileAsync(Guid publicIdentifier)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.PublicIdentifier == publicIdentifier && p.IsPublic && !p.IsDeleted);

        if (profile is null)
        {
            return null;
        }

        var displayName = await GetDisplayNameAsync(profile.UserId);

        return new UserProfileResponse
        {
            UserProfileId = profile.UserProfileId,
            UserId = profile.UserId,
            PublicIdentifier = profile.PublicIdentifier,
            IsPublic = profile.IsPublic,
            DisplayName = displayName
        };
    }

    public async Task UpdateProfileVisibilityAsync(string userId, bool isPublic)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

        if (profile is null)
        {
            return;
        }

        profile.IsPublic = isPublic;
        profile.UpdatedBy = userId;
        profile.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        await dbContext.SaveChangesAsync();
    }

    public async Task<string?> GetUserIdByPublicIdAsync(Guid publicIdentifier)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.PublicIdentifier == publicIdentifier && p.IsPublic && !p.IsDeleted);

        return profile?.UserId;
    }

    private async Task<string?> GetDisplayNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
        {
            return $"{user.FirstName} {user.LastName}".Trim();
        }

        return user.UserName;
    }
}
