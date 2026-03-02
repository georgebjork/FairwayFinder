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
}
