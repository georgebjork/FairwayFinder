using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

/// <summary>
/// Owns the whole password reset flow: minting the link, mailing it, and redeeming it.
/// </summary>
/// <remarks>
/// Both hosts reach this service — a golfer requests a reset from the app through the API, and an
/// admin sends the identical link on their behalf from the console. Keeping token generation,
/// encoding and the link format in one place is what lets a link minted by the admin console be
/// redeemed against the API: the two cannot drift, and the underlying Data Protection key ring is
/// shared (see <c>RegisterFeatureServices</c>).
/// </remarks>
public interface IPasswordResetService
{
    /// <summary>
    /// Sends a reset link to <paramref name="email"/> if it belongs to an account that can accept
    /// one. Reports success regardless, so the caller cannot use it to probe which addresses are
    /// registered; the real outcome is logged, not returned.
    /// </summary>
    Task SendResetLinkAsync(string email);

    /// <summary>
    /// Admin-initiated reset for a known user id, reporting the real outcome so the console can
    /// show it. Enumeration is not a concern here — the caller already holds the user list.
    /// </summary>
    /// <param name="userId">The golfer whose password is being reset.</param>
    /// <param name="requestedByAdminUserId">The acting admin, recorded in the audit log only.</param>
    Task<SendPasswordResetResult> SendResetLinkAsAdminAsync(string userId, string requestedByAdminUserId);

    /// <summary>
    /// Redeems a reset link and sets the new password. Returns one deliberately vague failure for
    /// an unknown email, a malformed token and an expired token alike, so a failed attempt reveals
    /// nothing about which of the three it was.
    /// </summary>
    Task<ResetPasswordResult> ResetPasswordAsync(string email, string token, string newPassword);
}
