using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class ProfileService : IProfileService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFriendService _friendService;

    public ProfileService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        UserManager<ApplicationUser> userManager,
        IFriendService friendService)
    {
        _dbContextFactory = dbContextFactory;
        _userManager = userManager;
        _friendService = friendService;
    }

    public async Task<UserProfileResponse> GetOrCreateProfileAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                PublicIdentifier = Guid.NewGuid(),
                IsPublic = false,
                CreatedBy = userId,
                UpdatedBy = userId,
                IsDeleted = false
            };

            dbContext.UserProfiles.Add(profile);
            await dbContext.SaveChangesAsync();
        }

        return ToResponse(profile, await _userManager.FindByIdAsync(profile.UserId));
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

        return ToResponse(profile, await _userManager.FindByIdAsync(profile.UserId));
    }

    public async Task<PublicProfileSettingsResult> UpdatePublicProfileAsync(
        string userId, PublicProfileSettingsRequest request, string actingUserId)
    {
        // Make sure the row exists: a golfer who never opened their profile has none yet.
        await GetOrCreateProfileAsync(userId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
        if (profile is null)
            return new PublicProfileSettingsResult(PublicProfileSettingsStatus.NotFound, "User not found.");

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? profile.PublicSlug
            : PublicSlugRules.Normalize(request.Slug);

        // First time public with no slug given: suggest one from the username.
        if (slug is null && request.IsPublic)
        {
            var handle = await dbContext.Users.Where(u => u.Id == userId).Select(u => u.Handle).FirstOrDefaultAsync();
            slug = PublicSlugRules.FromHandle(handle ?? string.Empty);
        }

        if (slug is not null && slug != profile.PublicSlug)
        {
            var error = PublicSlugRules.Validate(slug);
            if (error is not null)
                return new PublicProfileSettingsResult(PublicProfileSettingsStatus.Invalid, error);

            if (await dbContext.UserProfiles.AnyAsync(p => p.PublicSlug == slug && !p.IsDeleted && p.UserId != userId))
                return Taken();
        }

        profile.IsPublic = request.IsPublic;
        profile.PublicSlug = slug;
        profile.ShowStrokesGained = request.ShowStrokesGained;
        profile.ShowRecentRounds = request.ShowRecentRounds;
        profile.ShowCourseNames = request.ShowCourseNames;
        profile.UpdatedBy = actingUserId;

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Someone claimed the slug between the check and the save.
            return Taken();
        }

        return new PublicProfileSettingsResult(PublicProfileSettingsStatus.Ok);

        static PublicProfileSettingsResult Taken() =>
            new(PublicProfileSettingsStatus.Taken, "That profile link is taken.");
    }

    public async Task UpdatePreferredTeesAsync(string userId, PreferredTees preferredTees)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return;

        user.PreferredTees = preferredTees;
        await _userManager.UpdateAsync(user);
    }

    public async Task UpdateSgBaselineLevelAsync(string userId, BaselineLevel level)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return;

        user.SgBaselineLevel = (int)level;
        await _userManager.UpdateAsync(user);
    }

    public async Task<string?> GetUserIdByPublicIdAsync(Guid publicIdentifier)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.PublicIdentifier == publicIdentifier && p.IsPublic && !p.IsDeleted);

        return profile?.UserId;
    }

    public async Task<string?> GetUserIdByPublicIdIgnoringVisibilityAsync(Guid publicIdentifier)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.PublicIdentifier == publicIdentifier && !p.IsDeleted);

        return profile?.UserId;
    }

    public async Task<UserProfileResponse?> GetProfileForViewerAsync(Guid publicIdentifier, string? viewerUserId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.PublicIdentifier == publicIdentifier && !p.IsDeleted);

        if (profile is null)
        {
            return null;
        }

        var visible = profile.IsPublic
                      || (!string.IsNullOrEmpty(viewerUserId)
                          && (viewerUserId == profile.UserId
                              || await _friendService.AreFriendsAsync(viewerUserId, profile.UserId)));

        if (!visible)
        {
            return null;
        }

        return ToResponse(profile, await _userManager.FindByIdAsync(profile.UserId));
    }

    private static UserProfileResponse ToResponse(UserProfile profile, ApplicationUser? user) => new()
    {
        UserProfileId = profile.UserProfileId,
        UserId = profile.UserId,
        PublicIdentifier = profile.PublicIdentifier,
        IsPublic = profile.IsPublic,
        PublicSlug = profile.PublicSlug,
        ShowStrokesGained = profile.ShowStrokesGained,
        ShowRecentRounds = profile.ShowRecentRounds,
        ShowCourseNames = profile.ShowCourseNames,
        DisplayName = DisplayNameHelper.Build(user),
        Username = user?.Handle ?? string.Empty,
        Email = user?.Email,
        PreferredTees = user?.PreferredTees ?? PreferredTees.Mens,
        SgBaselineLevel = (BaselineLevel)(user?.SgBaselineLevel ?? 0)
    };

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is not null
           && ex.InnerException.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";
}
