using System.Diagnostics;
using FairwayFinder.Features.Diagnostics;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Shared.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;

namespace FairwayFinder.Features.Services.Email;

public class ResendEmailSender : IEmailSender
{
    private readonly IResend _resend;
    private readonly ILogger<ResendEmailSender> _logger;

    private const string FromAddress = "noreply@fairwayfinder.pro";

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

        await SendAsync(message, FairwayFinderDiagnostics.TagValues.EmailKindConfirmation);
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

        await SendAsync(message, FairwayFinderDiagnostics.TagValues.EmailKindPasswordReset);
    }

    // FairwayFinder is distributed through Apple's TestFlight beta program, so new
    // users must install TestFlight before they can install the app itself.
    private const string TestFlightInstallUrl = "https://apps.apple.com/app/testflight/id899247664";

    public async Task SendInvitationEmailAsync(string toEmail, string registrationLink, string? appInstallUrl)
    {
        // When the app install URL is configured, walk the user through the full
        // TestFlight setup step by step (most invitees have never used TestFlight).
        // Otherwise fall back to just the invitation link.
        var body = string.IsNullOrWhiteSpace(appInstallUrl)
            ? $"""
                <h2>You've been invited to FairwayFinder!</h2>
                <p>You've been invited to create a FairwayFinder account. Tap the link below on your iPhone to get started:</p>
                <p><a href="{registrationLink}">Accept your invitation</a></p>
                <p>This invitation will expire soon. If you weren't expecting this, you can safely ignore this email.</p>
                """
            : $"""
                <h2>You've been invited to FairwayFinder!</h2>
                <p>FairwayFinder is currently in beta, so it's delivered through Apple's free <strong>TestFlight</strong> app. Open this email on your iPhone and follow these three steps to get started:</p>
                <ol>
                    <li>
                        <p><strong>Install TestFlight.</strong> TestFlight is Apple's official app for trying beta apps. Tap the button below to install it from the App Store (it's free), then come back to this email.</p>
                        <p><a href="{TestFlightInstallUrl}">Step 1: Install TestFlight</a></p>
                    </li>
                    <li>
                        <p><strong>Install FairwayFinder.</strong> Once TestFlight is installed, tap the button below. TestFlight will open and let you install FairwayFinder.</p>
                        <p><a href="{appInstallUrl}">Step 2: Install FairwayFinder</a></p>
                    </li>
                    <li>
                        <p><strong>Create your account.</strong> After the app is installed, tap the button below to accept your invitation and finish registering.</p>
                        <p><a href="{registrationLink}">Step 3: Accept your invitation</a></p>
                    </li>
                </ol>
                <p>This invitation will expire soon. If you weren't expecting this, you can safely ignore this email.</p>
                """;

        var message = new EmailMessage
        {
            From = FromAddress,
            Subject = "You're invited to FairwayFinder",
            HtmlBody = body
        };
        message.To.Add(toEmail);

        await SendAsync(message, FairwayFinderDiagnostics.TagValues.EmailKindInvitation);
    }

    private async Task SendAsync(EmailMessage message, string kind)
    {
        var stopwatch = Stopwatch.StartNew();
        var tags = new TagList { { FairwayFinderDiagnostics.Tags.Kind, kind } };
        try
        {
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Email sent to {To} with subject '{Subject}'",
                message.To.FirstOrDefault(), message.Subject);
            FairwayFinderDiagnostics.EmailSent.Add(1, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} with subject '{Subject}'",
                message.To.FirstOrDefault(), message.Subject);
            FairwayFinderDiagnostics.EmailFailed.Add(1, new TagList
            {
                { FairwayFinderDiagnostics.Tags.Kind, kind },
                { FairwayFinderDiagnostics.Tags.Reason, ex.GetType().Name }
            });
            throw;
        }
        finally
        {
            FairwayFinderDiagnostics.EmailSendDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
        }
    }
}
