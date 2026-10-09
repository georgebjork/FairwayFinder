using System.Collections.Concurrent;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.IntegrationTests.Common.Fakes;

public enum SentEmailKind { Confirmation, PasswordReset, Invitation }

public sealed record SentEmail(SentEmailKind Kind, string To, string Link, string? AppInstallUrl = null);

/// <summary>
/// Captures outgoing mail instead of sending it, so tests can follow the invite and reset links a
/// real golfer would click.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent.ToArray();

    public void Clear() => _sent.Clear();

    public SentEmail LastTo(string email, SentEmailKind kind) =>
        Sent.Last(e => e.Kind == kind && string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase));

    public Task SendConfirmationEmailAsync(string toEmail, string confirmationLink)
    {
        _sent.Enqueue(new SentEmail(SentEmailKind.Confirmation, toEmail, confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        _sent.Enqueue(new SentEmail(SentEmailKind.PasswordReset, toEmail, resetLink));
        return Task.CompletedTask;
    }

    public Task SendInvitationEmailAsync(string toEmail, string registrationLink, string? appInstallUrl)
    {
        _sent.Enqueue(new SentEmail(SentEmailKind.Invitation, toEmail, registrationLink, appInstallUrl));
        return Task.CompletedTask;
    }
}
