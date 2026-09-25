namespace FairwayFinder.Features.Data;

/// <summary>
/// Result of sending a password reset link.
/// </summary>
/// <remarks>
/// Only the admin console reads <see cref="Error"/>. The golfer-facing API deliberately
/// discards it — see <see cref="Services.Interfaces.IPasswordResetService.SendResetLinkAsync"/>.
/// </remarks>
public class SendPasswordResetResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    public static SendPasswordResetResult Ok() => new() { Success = true };
    public static SendPasswordResetResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Result of redeeming a reset link and setting the new password.
/// </summary>
public class ResetPasswordResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    public static ResetPasswordResult Ok() => new() { Success = true };
    public static ResetPasswordResult Fail(string error) => new() { Success = false, Error = error };
}
