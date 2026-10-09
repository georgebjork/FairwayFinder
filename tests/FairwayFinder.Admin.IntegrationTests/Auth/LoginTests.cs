using System.Net;
using System.Text.RegularExpressions;
using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Identity;
using FairwayFinder.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Admin.IntegrationTests.Auth;

/// <summary>
/// The real Identity cookie sign-in through the static-SSR login form — antiforgery token, form
/// post, Secure cookie and all — without the test auth headers.
/// </summary>
public partial class LoginTests(AdminFactory factory) : AdminTestBase(factory)
{
    private const string AdminCookie = ".fairway.finder.admin";

    [Fact]
    public async Task Admin_signs_in_gets_the_auth_cookie_and_can_reach_the_console()
    {
        var admin = await Data.CreateUserAsync(roles: ApplicationRoles.Admin);
        using var browser = BrowserClient();

        var response = await SubmitLoginAsync(browser, admin.Email!, TestData.DefaultPassword, returnUrl: "/users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/users", response.Headers.Location?.PathAndQuery);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(AdminCookie) && c.Contains("secure"));

        await AssertStatusAsync(HttpStatusCode.OK, await browser.GetAsync("/users"));
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        var admin = await Data.CreateUserAsync(roles: ApplicationRoles.Admin);
        using var browser = BrowserClient();
        await SubmitLoginAsync(browser, admin.Email!, TestData.DefaultPassword);

        var logout = await browser.GetAsync("/authentication/logout");
        Assert.Equal("/login", logout.Headers.Location?.OriginalString);

        var after = await browser.GetAsync("/users");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.StartsWith("/login", after.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Golfers_without_the_admin_role_are_refused_at_the_door()
    {
        var golfer = await Data.CreateUserAsync(roles: ApplicationRoles.User);
        using var browser = BrowserClient();

        var response = await SubmitLoginAsync(browser, golfer.Email!, TestData.DefaultPassword);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Contains("restricted to administrators", await response.Content.ReadAsStringAsync());

        // The sign-in was undone, so the console is still closed to them.
        var console = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, console.StatusCode);
        Assert.StartsWith("/login", console.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Wrong_password_shows_an_error_and_issues_no_cookie()
    {
        var admin = await Data.CreateUserAsync(roles: ApplicationRoles.Admin);
        using var browser = BrowserClient();

        var response = await SubmitLoginAsync(browser, admin.Email!, "not-the-password");

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Contains("Invalid login attempt", await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
                     && cookies.Any(c => c.StartsWith(AdminCookie + "=") && !c.Contains("expires=Thu, 01 Jan 1970")));
    }

    [Fact]
    public async Task The_seeded_admin_from_configuration_can_sign_in()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var startup = scope.ServiceProvider.GetRequiredService<FairwayFinder.Admin.Services.IApplicationStartupService>();
            await startup.SeedDefaultUserAsync();
        }

        using var browser = BrowserClient();
        var response = await SubmitLoginAsync(browser, AdminFactory.SeedAdminEmail, AdminFactory.SeedAdminPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>Loads the login page for its antiforgery token, then posts the form as a browser would.</summary>
    private static async Task<HttpResponseMessage> SubmitLoginAsync(
        HttpClient browser, string email, string password, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/login" : $"/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";

        var page = await browser.GetAsync(url);
        await AssertStatusAsync(HttpStatusCode.OK, page);
        var token = AntiforgeryToken().Match(await page.Content.ReadAsStringAsync()).Groups["token"].Value;
        Assert.False(string.IsNullOrEmpty(token), "The login form did not render an antiforgery token.");

        return await browser.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
        }));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
