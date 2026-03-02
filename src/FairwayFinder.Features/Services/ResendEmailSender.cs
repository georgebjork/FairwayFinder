using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Shared.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;

namespace FairwayFinder.Features.Services;

public class ResendEmailSender : IEmailSender
{
    private readonly IResend _resend;
    private readonly ILogger<ResendEmailSender> _logger;

    private const string FromAddress = "noreply@fairwayfinder.app";

    public ResendEmailSender(IOptions<ResendSettings> settings, ILogger<ResendEmailSender> logger)
    {
        _resend = ResendClient.Create(settings.Value.ApiKey);
        _logger = logger;
    }

    public async Task SendConfirmationEmailAsync(string toEmail, string confirmationLink)
    {
        var message = new EmailMessage
        {
            From = FromAddress,
            Subject = "Confirm your FairwayFinder account",
            HtmlBody = $"""
                <h2>Welcome to FairwayFinder!</h2>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href="{confirmationLink}">Confirm Email</a></p>
                <p>If you didn't create an account, you can safely ignore this email.</p>
                """
        };
        message.To.Add(toEmail);

        await SendAsync(message);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        var message = new EmailMessage
        {
            From = FromAddress,
            Subject = "Reset your FairwayFinder password",
            HtmlBody = $"""
                <h2>Password Reset</h2>
                <p>Click the link below to reset your password:</p>
                <p><a href="{resetLink}">Reset Password</a></p>
                <p>If you didn't request a password reset, you can safely ignore this email.</p>
                """
        };
        message.To.Add(toEmail);

        await SendAsync(message);
    }

    private async Task SendAsync(EmailMessage message)
    {
        try
        {
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Email sent to {To} with subject '{Subject}'",
                message.To.FirstOrDefault(), message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} with subject '{Subject}'",
                message.To.FirstOrDefault(), message.Subject);
            throw;
        }
    }
}
