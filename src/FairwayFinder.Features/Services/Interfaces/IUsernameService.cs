using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IUsernameService
{
    /// <summary>
    /// A free default username built from the golfer's name. Call before <c>UserManager.CreateAsync</c>
    /// on every account-creation path — the column is required.
    /// </summary>
    Task<string> GenerateDefaultAsync(string? firstName, string? lastName);

    /// <summary>Whether <paramref name="raw"/> is valid and free, ignoring <paramref name="excludingUserId"/>'s own.</summary>
    Task<UsernameAvailability> CheckAvailabilityAsync(string? raw, string? excludingUserId = null);

    /// <summary>
    /// Sets <paramref name="userId"/>'s username. <paramref name="userId"/> is the account being
    /// changed — when an admin acts on someone else, pass the target user, not the admin.
    /// </summary>
    Task<UsernameChangeResult> ChangeUsernameAsync(string userId, string? raw);
}
