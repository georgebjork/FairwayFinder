using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Features.Tests.Helpers;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// Resending an invite from the admin console. The rule under test is that a resend is a repair
/// action on an existing invitation, not a new one: the identifier never changes, so a link already
/// sitting in the recipient's inbox keeps working, and the validity window rolls forward so an
/// expired invite becomes usable again instead of mailing out a link that fails validation.
/// Claimed and revoked invites are not resendable.
/// </summary>
public class UserInvitationServiceResendTests
{
    private const string AdminId = "admin-user";
    private const string OriginalSenderId = "original-sender";
    private const string RegistrationUrl = "https://fairwayfinder.pro/register";

    /// <summary>
    /// Records what was mailed, and can be told to fail so the send-failure path is reachable.
    /// The confirmation and password-reset members throw: resend never touches them.
    /// </summary>
    private sealed class RecordingEmailSender : IEmailSender
    {
        public bool ShouldThrow { get; set; }
        public List<(string ToEmail, string Link, string? InstallUrl)> Sent { get; } = new();

        public Task SendInvitationEmailAsync(string toEmail, string registrationLink, string? appInstallUrl)
        {
            if (ShouldThrow) throw new InvalidOperationException("smtp down");
            Sent.Add((toEmail, registrationLink, appInstallUrl));
            return Task.CompletedTask;
        }

        public Task SendConfirmationEmailAsync(string toEmail, string confirmationLink)
            => throw new NotSupportedException();

        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// <c>UserInvitationService</c> takes a <see cref="UserManager{TUser}"/> for the create path's
    /// duplicate-account check. Resend never calls it, so the store throws to make it obvious if
    /// that ever changes.
    /// </summary>
    private sealed class ThrowingUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser u, CancellationToken c) => throw new NotSupportedException();
        public Task<string?> GetUserNameAsync(ApplicationUser u, CancellationToken c) => throw new NotSupportedException();
        public Task SetUserNameAsync(ApplicationUser u, string? n, CancellationToken c) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser u, CancellationToken c) => throw new NotSupportedException();
        public Task SetNormalizedUserNameAsync(ApplicationUser u, string? n, CancellationToken c) => throw new NotSupportedException();
        public Task<IdentityResult> CreateAsync(ApplicationUser u, CancellationToken c) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser u, CancellationToken c) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser u, CancellationToken c) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken c) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken c) => throw new NotSupportedException();
    }

    private sealed record Harness(
        UserInvitationService Service,
        InMemoryDbContextFactory Factory,
        RecordingEmailSender Email);

    private static Harness Create(string dbName)
    {
        var factory = new InMemoryDbContextFactory(dbName);
        var email = new RecordingEmailSender();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Invites:RegistrationUrlBase"] = RegistrationUrl,
                ["Invites:AppInstallUrl"] = "https://testflight.apple.com/join/test"
            })
            .Build();

        var userManager = new UserManager<ApplicationUser>(
            new ThrowingUserStore(), null!, null!, null!, null!, null!, null!, null!, null!);

        return new Harness(
            new UserInvitationService(factory, userManager, email, config, NullLogger<UserInvitationService>.Instance),
            factory,
            email);
    }

    private static async Task<int> SeedInviteAsync(
        InMemoryDbContextFactory factory,
        string identifier,
        DateTime expiresOn,
        DateTime? claimedOn = null,
        bool isDeleted = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var invite = new UserInvitation
        {
            InvitationIdentifier = identifier,
            SentToEmail = "golfer@example.com",
            SentByUser = OriginalSenderId,
            IsDeleted = isDeleted,
            ClaimedOn = claimedOn,
            ExpiresOn = expiresOn,
            CreatedBy = OriginalSenderId,
            UpdatedBy = OriginalSenderId
        };
        db.UserInvitations.Add(invite);
        await db.SaveChangesAsync();
        return invite.Id;
    }

    [Fact]
    public async Task Resending_an_expired_invite_rolls_the_expiry_forward_and_mails_the_same_code()
    {
        var h = Create(nameof(Resending_an_expired_invite_rolls_the_expiry_forward_and_mails_the_same_code));
        var id = await SeedInviteAsync(h.Factory, "abc123", DateTime.UtcNow.AddDays(-3));

        var result = await h.Service.ResendInviteAsync(id, AdminId);

        Assert.True(result.Success);

        await using var db = await h.Factory.CreateDbContextAsync();
        var invite = db.UserInvitations.Single(i => i.Id == id);

        Assert.True(invite.ExpiresOn > DateTime.UtcNow, "an expired invite must become valid again");
        Assert.Equal("abc123", invite.InvitationIdentifier);
        Assert.Equal(AdminId, invite.UpdatedBy);

        var sent = Assert.Single(h.Email.Sent);
        Assert.Equal("golfer@example.com", sent.ToEmail);
        Assert.Equal($"{RegistrationUrl}?code=abc123", sent.Link);
    }

    [Fact]
    public async Task Resending_leaves_the_original_sender_and_creator_untouched()
    {
        var h = Create(nameof(Resending_leaves_the_original_sender_and_creator_untouched));
        var id = await SeedInviteAsync(h.Factory, "keep-me", DateTime.UtcNow.AddDays(5));

        await h.Service.ResendInviteAsync(id, AdminId);

        await using var db = await h.Factory.CreateDbContextAsync();
        var invite = db.UserInvitations.Single(i => i.Id == id);

        Assert.Equal(OriginalSenderId, invite.SentByUser);
        Assert.Equal(OriginalSenderId, invite.CreatedBy);
        Assert.Equal(AdminId, invite.UpdatedBy);
    }

    [Fact]
    public async Task A_claimed_invite_cannot_be_resent()
    {
        var h = Create(nameof(A_claimed_invite_cannot_be_resent));
        var id = await SeedInviteAsync(h.Factory, "claimed", DateTime.UtcNow.AddDays(5), claimedOn: DateTime.UtcNow.AddDays(-1));

        var result = await h.Service.ResendInviteAsync(id, AdminId);

        Assert.False(result.Success);
        Assert.Contains("claimed", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(h.Email.Sent);
    }

    [Fact]
    public async Task A_revoked_invite_cannot_be_resent()
    {
        var h = Create(nameof(A_revoked_invite_cannot_be_resent));
        var id = await SeedInviteAsync(h.Factory, "revoked", DateTime.UtcNow.AddDays(5), isDeleted: true);

        var result = await h.Service.ResendInviteAsync(id, AdminId);

        Assert.False(result.Success);
        Assert.Empty(h.Email.Sent);
    }

    [Fact]
    public async Task An_unknown_invite_id_is_reported_rather_than_throwing()
    {
        var h = Create(nameof(An_unknown_invite_id_is_reported_rather_than_throwing));

        var result = await h.Service.ResendInviteAsync(9999, AdminId);

        Assert.False(result.Success);
        Assert.Empty(h.Email.Sent);
    }

    [Fact]
    public async Task A_failed_send_is_reported_but_the_extended_expiry_is_kept()
    {
        var h = Create(nameof(A_failed_send_is_reported_but_the_extended_expiry_is_kept));
        var id = await SeedInviteAsync(h.Factory, "mail-fails", DateTime.UtcNow.AddDays(-10));
        h.Email.ShouldThrow = true;

        var result = await h.Service.ResendInviteAsync(id, AdminId);

        Assert.False(result.Success);
        Assert.Contains("email", result.Error, StringComparison.OrdinalIgnoreCase);

        // The window was already committed, so a retry does not need to repair anything.
        await using var db = await h.Factory.CreateDbContextAsync();
        Assert.True(db.UserInvitations.Single(i => i.Id == id).ExpiresOn > DateTime.UtcNow);
    }
}
