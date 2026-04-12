using FairwayFinder.Features.Data;
using FairwayFinder.Identity;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IProfileService
{
    /// <summary>
    /// Gets the user's profile, creating one if it doesn't exist.
    /// New profiles default to IsPublic = false with a freshly generated PublicIdentifier.
    /// </summary>
    Task<UserProfileResponse> GetOrCreateProfileAsync(string userId);

    /// <summary>
    /// Looks up a profile by its public GUID.
    /// Returns null if not found, deleted, or not public.
    /// </summary>
    Task<UserProfileResponse?> GetPublicProfileAsync(Guid publicIdentifier);

    /// <summary>
    /// Toggles the user's profile between public and private.
    /// </summary>
    Task UpdateProfileVisibilityAsync(string userId, bool isPublic);

    /// <summary>
    /// Updates the user's preferred tee type used to filter teebox dropdowns.
    /// </summary>
    Task UpdatePreferredTeesAsync(string userId, PreferredTees preferredTees);

    /// <summary>
    /// Resolves a public identifier to a user ID, but only if the profile is public.
    /// Returns null if the profile doesn't exist or is private.
    /// </summary>
    Task<string?> GetUserIdByPublicIdAsync(Guid publicIdentifier);
}
