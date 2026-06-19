namespace FairwayFinder.Features.Services.Interfaces;

public interface IEmailSender
{
    /// <summary>
    /// Sends an email confirmation link to the specified address.
    /// </summary>
    Task SendConfirmationEmailAsync(string toEmail, string confirmationLink);

    /// <summary>
    /// Sends a password reset link to the specified address.
    /// </summary>
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);

    /// <summary>
    /// Sends an invitation link inviting the recipient to register an account.
    /// When <paramref name="appInstallUrl"/> is provided, the email also offers an install link
    /// (e.g. TestFlight or the App Store) for recipients who don't have the app yet.
    /// </summary>
    Task SendInvitationEmailAsync(string toEmail, string registrationLink, string? appInstallUrl);
}
