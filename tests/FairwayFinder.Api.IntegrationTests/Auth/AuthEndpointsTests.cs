using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FairwayFinder.Api.Auth;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using FairwayFinder.Identity;
using FairwayFinder.IntegrationTests.Common;
using FairwayFinder.IntegrationTests.Common.Fakes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FairwayFinder.Api.IntegrationTests.Auth;

/// <summary>
/// Login, token refresh and revocation, invite-only registration and password reset — every way
/// a golfer gets (or loses) a session on the app.
/// </summary>
public class AuthEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    // ── Login ──

    [Fact]
    public async Task Login_with_valid_credentials_returns_tokens_that_authorize_api_calls()
    {
        var user = await Data.CreateUserAsync(roles: ApplicationRoles.User);

        var login = await LoginAsync(user.Email!, TestData.DefaultPassword);

        Assert.Equal(user.Id, login.UserId);
        Assert.Equal(user.Email, login.Email);
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.True(login.ExpiresAt > DateTime.UtcNow);

        using var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        await AssertStatusAsync(HttpStatusCode.OK, await client.GetAsync("/api/profile"));
    }

    [Fact]
    public async Task Login_access_token_carries_identity_and_role_claims()
    {
        var user = await Data.CreateUserAsync(roles: ApplicationRoles.Admin);

        var login = await LoginAsync(user.Email!, TestData.DefaultPassword);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);

        Assert.Equal(TestSettings.JwtIssuer, jwt.Issuer);
        Assert.Contains(TestSettings.JwtAudience, jwt.Audiences);
        Assert.Contains(jwt.Claims, c => c.Value == user.Id);
        Assert.Contains(jwt.Claims, c => c.Value == ApplicationRoles.Admin);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var user = await Data.CreateUserAsync();

        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = user.Email!, Password = "not-the-password" });

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
    }

    [Fact]
    public async Task Login_for_unknown_email_is_unauthorized()
    {
        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@test.fairwayfinder.pro", Password = TestData.DefaultPassword });

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account_even_for_the_right_password()
    {
        var user = await Data.CreateUserAsync();
        using var client = AnonymousClient();

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email!, Password = "wrong" });

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = user.Email!, Password = TestData.DefaultPassword });

        await AssertStatusAsync(HttpStatusCode.Locked, response);
    }

    // ── Bearer token validation ──

    [Fact]
    public async Task Protected_endpoint_without_a_token_is_unauthorized()
    {
        using var client = AnonymousClient();
        await AssertStatusAsync(HttpStatusCode.Unauthorized, await client.GetAsync("/api/rounds"));
    }

    [Fact]
    public async Task Expired_access_token_is_rejected()
    {
        var user = await Data.CreateUserAsync(roles: ApplicationRoles.User);
        var token = MintToken(user.Id, expires: DateTime.UtcNow.AddMinutes(-1), TestSettings.JwtSecret);

        using var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await AssertStatusAsync(HttpStatusCode.Unauthorized, await client.GetAsync("/api/profile"));
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var user = await Data.CreateUserAsync(roles: ApplicationRoles.User);
        var token = MintToken(user.Id, expires: DateTime.UtcNow.AddMinutes(5),
            "a-completely-different-signing-key-of-sufficient-length");

        using var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await AssertStatusAsync(HttpStatusCode.Unauthorized, await client.GetAsync("/api/profile"));
    }

    // ── Refresh and logout ──

    [Fact]
    public async Task Refresh_rotates_the_refresh_token()
    {
        var user = await Data.CreateUserAsync(roles: ApplicationRoles.User);
        var login = await LoginAsync(user.Email!, TestData.DefaultPassword);

        using var client = AnonymousClient();
        var refreshed = await ReadAsync<LoginResponse>(await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = login.RefreshToken }));

        Assert.Equal(user.Id, refreshed.UserId);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        // The new refresh token works in turn.
        var again = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = refreshed.RefreshToken });
        await AssertStatusAsync(HttpStatusCode.OK, again);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_every_session_for_that_user()
    {
        var user = await Data.CreateUserAsync(roles: ApplicationRoles.User);
        var phone = await LoginAsync(user.Email!, TestData.DefaultPassword);
        var tablet = await LoginAsync(user.Email!, TestData.DefaultPassword);

        using var client = AnonymousClient();
        var rotated = await ReadAsync<LoginResponse>(await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = phone.RefreshToken }));

        // An attacker replays the stolen, already-rotated token.
        var replay = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = phone.RefreshToken });
        await AssertStatusAsync(HttpStatusCode.Unauthorized, replay);

        // Every live token for the user is now dead — including other devices.
        foreach (var token in new[] { rotated.RefreshToken, tablet.RefreshToken })
        {
            var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = token });
            await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
        }

        await using var db = Db();
        Assert.True(await db.RefreshTokens.Where(t => t.UserId == user.Id).AllAsync(t => t.RevokedAt != null));
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_is_unauthorized()
    {
        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) });

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var user = await Data.CreateUserAsync(roles: ApplicationRoles.User);
        var login = await LoginAsync(user.Email!, TestData.DefaultPassword);

        using var client = AnonymousClient();
        await AssertStatusAsync(HttpStatusCode.NoContent, await client.PostAsJsonAsync("/api/auth/logout",
            new RefreshRequest { RefreshToken = login.RefreshToken }));

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = login.RefreshToken });
        await AssertStatusAsync(HttpStatusCode.Unauthorized, refresh);
    }

    // ── Invite-only registration ──

    [Fact]
    public async Task Invite_lookup_reports_a_valid_invite_and_its_email()
    {
        var invite = await Data.CreateInvitationAsync("invitee@test.fairwayfinder.pro");

        using var client = AnonymousClient();
        var result = await GetAsync<InviteValidationResult>(client, $"/api/auth/invites/{invite.InvitationIdentifier}");

        Assert.True(result.Valid);
        Assert.Equal("invitee@test.fairwayfinder.pro", result.Email);
    }

    [Fact]
    public async Task Invite_lookup_rejects_unknown_and_expired_codes()
    {
        var expired = await Data.CreateInvitationAsync("late@test.fairwayfinder.pro", expiresOn: DateTime.UtcNow.AddDays(-1));

        using var client = AnonymousClient();
        var unknown = await GetAsync<InviteValidationResult>(client, "/api/auth/invites/not-a-code");
        var late = await GetAsync<InviteValidationResult>(client, $"/api/auth/invites/{expired.InvitationIdentifier}");

        Assert.False(unknown.Valid);
        Assert.False(late.Valid);
    }

    [Fact]
    public async Task Register_with_an_invite_creates_a_user_in_the_user_role_with_a_profile_and_claims_the_invite()
    {
        const string email = "newgolfer@test.fairwayfinder.pro";
        var invite = await Data.CreateInvitationAsync(email);

        using var client = AnonymousClient();
        var login = await ReadAsync<LoginResponse>(await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Code = invite.InvitationIdentifier,
            FirstName = " New ",
            LastName = "Golfer",
            Password = "a-long-password",
            PreferredTees = 0,
        }));

        Assert.Equal(email, login.Email);
        Assert.Equal("New", login.FirstName);

        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, ApplicationRoles.User));

        await using var db = Db();
        Assert.True(await db.UserProfiles.AnyAsync(p => p.UserId == user.Id));
        Assert.NotNull((await db.UserInvitations.SingleAsync(i => i.Id == invite.Id)).ClaimedOn);

        // The invite is single use.
        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Code = invite.InvitationIdentifier,
            FirstName = "Again",
            LastName = "Golfer",
            Password = "a-long-password",
        });
        await AssertStatusAsync(HttpStatusCode.BadRequest, second);
    }

    [Fact]
    public async Task Register_without_a_valid_invite_is_rejected()
    {
        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Code = "made-up",
            FirstName = "Sneaky",
            LastName = "Golfer",
            Password = "a-long-password",
        });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task Register_with_an_invalid_payload_returns_validation_problem_details()
    {
        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Code = "",
            FirstName = "",
            LastName = "",
            Password = "short",
        });

        var problem = await ReadAsync<JsonElement>(response, HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Password", out _));
        Assert.True(errors.TryGetProperty("FirstName", out _));
    }

    // ── Password reset ──

    [Fact]
    public async Task Forgot_password_emails_a_link_that_resets_the_password()
    {
        var user = await Data.CreateUserAsync(roles: ApplicationRoles.User);

        using var client = AnonymousClient();
        await AssertStatusAsync(HttpStatusCode.NoContent, await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = user.Email! }));

        var email = Email.LastTo(user.Email!, SentEmailKind.PasswordReset);
        Assert.StartsWith(TestSettings.PasswordResetUrlBase, email.Link);

        var query = QueryHelpers.ParseQuery(new Uri(email.Link).Query);
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = query["email"]!,
            Token = query["token"]!,
            NewPassword = "brand-new-password",
        });
        await AssertStatusAsync(HttpStatusCode.NoContent, reset);

        await LoginAsync(user.Email!, "brand-new-password");
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_still_succeeds_but_sends_nothing()
    {
        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "nobody@test.fairwayfinder.pro" });

        await AssertStatusAsync(HttpStatusCode.NoContent, response);
        Assert.Empty(Email.Sent);
    }

    [Fact]
    public async Task Reset_password_with_a_bad_token_is_rejected()
    {
        var user = await Data.CreateUserAsync();
        var bogus = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("not-a-token"));

        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = user.Email!,
            Token = bogus,
            NewPassword = "brand-new-password",
        });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    private static string MintToken(string userId, DateTime expires, string secret)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestSettings.JwtIssuer,
            audience: TestSettings.JwtAudience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, ApplicationRoles.User)],
            notBefore: expires.AddMinutes(-10),
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
