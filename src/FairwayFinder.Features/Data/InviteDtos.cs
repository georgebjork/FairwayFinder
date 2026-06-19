namespace FairwayFinder.Features.Data;

/// <summary>
/// Result of validating an invitation code (used by the anonymous registration screen).
/// </summary>
public class InviteValidationResult
{
    public bool Valid { get; set; }
    public string? Email { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Result of creating + sending an invite.
/// </summary>
public class CreateInviteResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Admin-facing view of an invitation.
/// </summary>
public class PendingInviteDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateOnly CreatedOn { get; set; }
    public DateOnly ExpiresOn { get; set; }
    public DateOnly? ClaimedOn { get; set; }
    public bool IsExpired { get; set; }
}

/// <summary>
/// Admin request to send an invitation to an email address.
/// </summary>
public class SendInviteRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Anonymous request to register an account using an invitation code.
/// The email is taken from the invitation server-side, not the request.
/// </summary>
public class RegisterRequest
{
    public string Code { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int PreferredTees { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
}
