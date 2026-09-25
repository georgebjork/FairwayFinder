using System.Web;
using FairwayFinder.Data;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// The password reset flow, with the cross-host case as the centrepiece.
///
/// An admin sends a reset link from the admin console and the golfer redeems it against the API —
/// two separately deployed processes. Identity's reset token is a Data Protection payload, so that
/// only works while both hosts share a key ring *and* a Data Protection application name. Each is
/// invisible in normal single-host testing and breaks nothing until a real admin sends a real link,
/// so both are pinned here: <see cref="A_link_minted_by_the_admin_console_is_redeemable_against_the_api"/>
/// covers the working arrangement, and the two tests after it fail if either half is dropped.
/// </summary>
public class PasswordResetServiceTests
{
    private const string ResetUrlBase = "https://fairwayfinder.pro/reset-password";
    private const string AdminId = "admin-user";
    private const string GolferEmail = "golfer@example.com";

    private sealed class RecordingEmailSender : IEmailSender
    {
        public bool ShouldThrow { get; set; }
        public List<(string ToEmail, string Link)> Sent { get; } = new();

        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            if (ShouldThrow) throw new InvalidOperationException("resend is down");
            Sent.Add((toEmail, resetLink));
            return Task.CompletedTask;
        }

        public Task SendConfirmationEmailAsync(string toEmail, string confirmationLink)
            => throw new NotSupportedException();

        public Task SendInvitationEmailAsync(string toEmail, string registrationLink, string? appInstallUrl)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// One deployed process. Two hosts built over the same <paramref name="dbName"/> share a
    /// database, and therefore the Data Protection key ring, exactly as Admin and Api do in
    /// production; <paramref name="applicationName"/> is what <c>SetApplicationName</c> pins.
    /// </summary>
    private sealed class TestHost : IDisposable
    {
        private readonly ServiceProvider _provider;
        public RecordingEmailSender Email { get; } = new();

        public TestHost(string dbName, string applicationName = "FairwayFinder")
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:PasswordResetUrlBase"] = ResetUrlBase
                })
                .Build());

            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));

            services.AddDataProtection()
                .PersistKeysToDbContext<ApplicationDbContext>()
                .SetApplicationName(applicationName);

            services.AddIdentityCore<ApplicationUser>(o =>
                {
                    o.Password.RequiredLength = 8;
                    o.Password.RequireDigit = false;
                    o.Password.RequireNonAlphanumeric = false;
                    o.Password.RequireUppercase = false;
                    o.Password.RequireLowercase = false;
                    o.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddSingleton<IEmailSender>(Email);

            _provider = services.BuildServiceProvider();
        }

        /// <summary>Runs against a fresh scope, as a request would.</summary>
        public async Task<T> RunAsync<T>(Func<IPasswordResetService, UserManager<ApplicationUser>, Task<T>> action)
        {
            using var scope = _provider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var service = new PasswordResetService(
                userManager,
                Email,
                scope.ServiceProvider.GetRequiredService<IConfiguration>(),
                NullLogger<PasswordResetService>.Instance);

            return await action(service, userManager);
        }

        public Task<string> SeedUserAsync(string email, bool emailConfirmed = true, string password = "original-password")
            => RunAsync(async (_, userManager) =>
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = emailConfirmed,
                    FirstName = "Test",
                    LastName = "Golfer"
                };

                var result = await userManager.CreateAsync(user, password);
                Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
                return user.Id;
            });

        public Task<bool> PasswordIsAsync(string email, string password)
            => RunAsync(async (_, userManager) =>
            {
                var user = await userManager.FindByEmailAsync(email);
                return user is not null && await userManager.CheckPasswordAsync(user, password);
            });

        public void Dispose() => _provider.Dispose();
    }

    /// <summary>Pulls the token back out of the emailed link the way the iOS app does.</summary>
    private static (string Email, string Token) ParseLink(string link)
    {
        var query = HttpUtility.ParseQueryString(new Uri(link).Query);
        return (query["email"]!, query["token"]!);
    }

    // ── The cross-host contract ─────────────────────────────────

    [Fact]
    public async Task A_link_minted_by_the_admin_console_is_redeemable_against_the_api()
    {
        var db = nameof(A_link_minted_by_the_admin_console_is_redeemable_against_the_api);
        using var admin = new TestHost(db);
        using var api = new TestHost(db);

        var userId = await admin.SeedUserAsync(GolferEmail);

        var sendResult = await admin.RunAsync((svc, _) => svc.SendResetLinkAsAdminAsync(userId, AdminId));
        Assert.True(sendResult.Success);

        var (email, token) = ParseLink(Assert.Single(admin.Email.Sent).Link);

        // The redemption happens in the other process entirely.
        var resetResult = await api.RunAsync((svc, _) => svc.ResetPasswordAsync(email, token, "brand-new-password"));

        Assert.True(resetResult.Success, resetResult.Error);
        Assert.True(await api.PasswordIsAsync(GolferEmail, "brand-new-password"));
    }

    [Fact]
    public async Task A_host_with_a_different_application_name_cannot_redeem_the_link()
    {
        var db = nameof(A_host_with_a_different_application_name_cannot_redeem_the_link);
        using var admin = new TestHost(db, applicationName: "FairwayFinder");
        // Same shared key ring, but the discriminator SetApplicationName pins is part of the
        // purpose chain — which is why relying on the default (derived from the content root
        // path, and so different per host) silently breaks every admin-sent link.
        using var mismatched = new TestHost(db, applicationName: "FairwayFinder.Api");

        var userId = await admin.SeedUserAsync(GolferEmail);
        await admin.RunAsync((svc, _) => svc.SendResetLinkAsAdminAsync(userId, AdminId));
        var (email, token) = ParseLink(Assert.Single(admin.Email.Sent).Link);

        var result = await mismatched.RunAsync((svc, _) => svc.ResetPasswordAsync(email, token, "brand-new-password"));

        Assert.False(result.Success);
        Assert.True(await mismatched.PasswordIsAsync(GolferEmail, "original-password"));
    }

    [Fact]
    public async Task A_host_with_its_own_key_ring_cannot_redeem_the_link()
    {
        var shared = nameof(A_host_with_its_own_key_ring_cannot_redeem_the_link);
        using var admin = new TestHost(shared);

        var userId = await admin.SeedUserAsync(GolferEmail);
        await admin.RunAsync((svc, _) => svc.SendResetLinkAsAdminAsync(userId, AdminId));
        var (email, token) = ParseLink(Assert.Single(admin.Email.Sent).Link);

        // A separate database stands in for the container-local key ring each host would keep
        // if PersistKeysToDbContext were dropped. The user is seeded again so the only thing
        // that differs from the passing case is the key ring.
        using var isolated = new TestHost(shared + "-isolated-keyring");
        await isolated.SeedUserAsync(GolferEmail);

        var result = await isolated.RunAsync((svc, _) => svc.ResetPasswordAsync(email, token, "brand-new-password"));

        Assert.False(result.Success);
        Assert.True(await isolated.PasswordIsAsync(GolferEmail, "original-password"));
    }

    // ── Link generation ─────────────────────────────────────────

    [Fact]
    public async Task The_emailed_link_carries_the_configured_base_and_a_url_safe_token()
    {
        using var host = new TestHost(nameof(The_emailed_link_carries_the_configured_base_and_a_url_safe_token));
        await host.SeedUserAsync(GolferEmail);

        await host.RunAsync(async (svc, _) => { await svc.SendResetLinkAsync(GolferEmail); return 0; });

        var sent = Assert.Single(host.Email.Sent);
        Assert.Equal(GolferEmail, sent.ToEmail);
        Assert.StartsWith($"{ResetUrlBase}?", sent.Link);

        var (email, token) = ParseLink(sent.Link);
        Assert.Equal(GolferEmail, email);
        Assert.NotEmpty(token);

        // Base64url, so the token survives the query string without escaping.
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    // ── Redemption rules ────────────────────────────────────────

    [Fact]
    public async Task A_link_cannot_be_redeemed_twice()
    {
        using var host = new TestHost(nameof(A_link_cannot_be_redeemed_twice));
        await host.SeedUserAsync(GolferEmail);
        await host.RunAsync(async (svc, _) => { await svc.SendResetLinkAsync(GolferEmail); return 0; });
        var (email, token) = ParseLink(Assert.Single(host.Email.Sent).Link);

        var first = await host.RunAsync((svc, _) => svc.ResetPasswordAsync(email, token, "first-new-password"));
        Assert.True(first.Success, first.Error);

        // Resetting rolls the security stamp, which is what retires the token.
        var second = await host.RunAsync((svc, _) => svc.ResetPasswordAsync(email, token, "second-new-password"));

        Assert.False(second.Success);
        Assert.True(await host.PasswordIsAsync(GolferEmail, "first-new-password"));
    }

    [Fact]
    public async Task A_malformed_token_is_reported_rather_than_throwing()
    {
        using var host = new TestHost(nameof(A_malformed_token_is_reported_rather_than_throwing));
        await host.SeedUserAsync(GolferEmail);

        var result = await host.RunAsync((svc, _) => svc.ResetPasswordAsync(GolferEmail, "not-a-real-token!!", "brand-new-password"));

        Assert.False(result.Success);
        Assert.Contains("invalid or has expired", result.Error);
        Assert.True(await host.PasswordIsAsync(GolferEmail, "original-password"));
    }

    [Fact]
    public async Task An_unknown_email_gets_the_same_failure_as_a_bad_token()
    {
        using var host = new TestHost(nameof(An_unknown_email_gets_the_same_failure_as_a_bad_token));
        await host.SeedUserAsync(GolferEmail);
        await host.RunAsync(async (svc, _) => { await svc.SendResetLinkAsync(GolferEmail); return 0; });
        var (_, token) = ParseLink(Assert.Single(host.Email.Sent).Link);

        var unknown = await host.RunAsync((svc, _) => svc.ResetPasswordAsync("nobody@example.com", token, "brand-new-password"));
        var badToken = await host.RunAsync((svc, _) => svc.ResetPasswordAsync(GolferEmail, "bogus", "brand-new-password"));

        Assert.False(unknown.Success);
        Assert.False(badToken.Success);

        // Identical wording, so a failed attempt says nothing about which addresses are registered.
        Assert.Equal(badToken.Error, unknown.Error);
    }

    [Fact]
    public async Task A_rejected_password_is_reported_separately_from_a_rejected_link()
    {
        using var host = new TestHost(nameof(A_rejected_password_is_reported_separately_from_a_rejected_link));
        await host.SeedUserAsync(GolferEmail);
        await host.RunAsync(async (svc, _) => { await svc.SendResetLinkAsync(GolferEmail); return 0; });
        var (email, token) = ParseLink(Assert.Single(host.Email.Sent).Link);

        var result = await host.RunAsync((svc, _) => svc.ResetPasswordAsync(email, token, "short"));

        Assert.False(result.Success);
        // "get a new link" and "pick a better password" are different instructions to the user.
        Assert.DoesNotContain("invalid or has expired", result.Error);
    }

    // ── Who gets told what ──────────────────────────────────────

    [Fact]
    public async Task An_unknown_email_is_reported_as_success_and_sends_nothing()
    {
        using var host = new TestHost(nameof(An_unknown_email_is_reported_as_success_and_sends_nothing));

        // No throw, no signal to the caller — the point of the blanket response.
        await host.RunAsync(async (svc, _) => { await svc.SendResetLinkAsync("nobody@example.com"); return 0; });

        Assert.Empty(host.Email.Sent);
    }

    [Fact]
    public async Task An_unconfirmed_email_is_silent_to_the_golfer_but_explained_to_an_admin()
    {
        using var host = new TestHost(nameof(An_unconfirmed_email_is_silent_to_the_golfer_but_explained_to_an_admin));
        var userId = await host.SeedUserAsync(GolferEmail, emailConfirmed: false);

        await host.RunAsync(async (svc, _) => { await svc.SendResetLinkAsync(GolferEmail); return 0; });
        Assert.Empty(host.Email.Sent);

        var adminResult = await host.RunAsync((svc, _) => svc.SendResetLinkAsAdminAsync(userId, AdminId));

        Assert.False(adminResult.Success);
        Assert.Contains("not confirmed", adminResult.Error);
        Assert.Empty(host.Email.Sent);
    }

    [Fact]
    public async Task A_send_failure_is_swallowed_for_the_golfer_and_surfaced_to_an_admin()
    {
        using var host = new TestHost(nameof(A_send_failure_is_swallowed_for_the_golfer_and_surfaced_to_an_admin));
        var userId = await host.SeedUserAsync(GolferEmail);
        host.Email.ShouldThrow = true;

        // Must not throw: a failure here would answer differently for a real address than an
        // unknown one, which is the leak the blanket response exists to prevent.
        await host.RunAsync(async (svc, _) => { await svc.SendResetLinkAsync(GolferEmail); return 0; });

        var adminResult = await host.RunAsync((svc, _) => svc.SendResetLinkAsAdminAsync(userId, AdminId));

        Assert.False(adminResult.Success);
        Assert.Contains("failed to send", adminResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_admin_sending_to_a_deleted_user_is_told_so()
    {
        using var host = new TestHost(nameof(An_admin_sending_to_a_deleted_user_is_told_so));

        var result = await host.RunAsync((svc, _) => svc.SendResetLinkAsAdminAsync("no-such-user", AdminId));

        Assert.False(result.Success);
        Assert.Contains("no longer exists", result.Error);
        Assert.Empty(host.Email.Sent);
    }
}
