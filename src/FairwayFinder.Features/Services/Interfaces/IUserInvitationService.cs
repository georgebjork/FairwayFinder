using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IUserInvitationService
{
    /// <summary>
    /// Creates an invitation for the given email and sends the registration link.
    /// Rejects emails that already have an account or an active (unclaimed, non-expired) invite.
    /// </summary>
    Task<CreateInviteResult> CreateAndSendInviteAsync(string email, string sentByUserId);

    /// <summary>
    /// Validates an invitation code. Valid only if it exists, isn't deleted, isn't claimed,
    /// and hasn't expired. Returns the invited email when valid.
    /// </summary>
    Task<InviteValidationResult> ValidateInviteAsync(string code);

    /// <summary>
    /// Marks an invitation as claimed. Returns false if the code is no longer claimable.
    /// </summary>
    Task<bool> ClaimInviteAsync(string code);

    /// <summary>
    /// Returns all non-deleted invitations, newest first.
    /// </summary>
    Task<List<PendingInviteDto>> GetPendingInvitesAsync();

    /// <summary>
    /// Re-sends the invitation email for an existing unclaimed invite, rolling its expiry forward
    /// so an already-expired invite becomes usable again. The invitation code is unchanged, so a
    /// link already sitting in the recipient's inbox keeps working.
    /// </summary>
    Task<CreateInviteResult> ResendInviteAsync(int id, string resentByUserId);

    /// <summary>
    /// Soft-deletes (revokes) an invitation.
    /// </summary>
    Task<bool> RevokeInviteAsync(int id, string revokedByUserId);
}
