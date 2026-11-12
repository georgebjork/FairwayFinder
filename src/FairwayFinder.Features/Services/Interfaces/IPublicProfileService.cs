using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IPublicProfileService
{
    /// <summary>
    /// The public stats page for <paramref name="slug"/> (case-insensitive). Null when no profile has
    /// that slug <b>or</b> the profile isn't public, so a caller can't tell the two apart.
    /// </summary>
    Task<PublicProfileResponse?> GetBySlugAsync(string slug);
}
