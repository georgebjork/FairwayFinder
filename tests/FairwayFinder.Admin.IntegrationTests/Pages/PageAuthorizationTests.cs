using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Admin.Startup;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace FairwayFinder.Admin.IntegrationTests.Pages;

/// <summary>
/// Every routable page in the console is discovered by reflection, so a page added tomorrow is
/// covered without touching this file: it must carry the AdminOnly policy, bounce anonymous and
/// non-admin visitors to the right place, and let admins through.
/// </summary>
public partial class PageAuthorizationTests(AdminFactory factory) : AdminTestBase(factory)
{
    /// <summary>Pages that are reachable without the Admin role, on purpose.</summary>
    private static readonly HashSet<string> PublicRoutes = ["/login", "/Error", "/not-found", "/access-denied"];

    public static TheoryData<string> AdminRoutes() => new(AllRoutes().Where(r => !PublicRoutes.Contains(r)));

    private static IEnumerable<string> AllRoutes() =>
        RoutablePages().SelectMany(p => p.GetCustomAttributes<RouteAttribute>().Select(r => r.Template)).Distinct().Order();

    private static IEnumerable<Type> RoutablePages() =>
        typeof(Program).Assembly.GetTypes().Where(t => t.GetCustomAttributes<RouteAttribute>().Any());

    [Fact]
    public void Every_page_except_the_public_ones_requires_the_admin_only_policy()
    {
        var violations = RoutablePages()
            .Where(page => !page.GetCustomAttributes<RouteAttribute>().All(r => PublicRoutes.Contains(r.Template)))
            .Where(page => !page.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Policy == Policies.AdminOnly))
            .Select(page => page.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            "Pages missing @attribute [Authorize(Policy = Policies.AdminOnly)]: " + string.Join(", ", violations));
    }

    [Fact]
    public void The_public_route_list_has_not_gone_stale()
    {
        var routes = AllRoutes().ToHashSet();
        Assert.True(PublicRoutes.All(routes.Contains), "A route in PublicRoutes no longer exists.");
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Anonymous_visitors_are_sent_to_login(string route)
    {
        var url = Resolve(route);
        using var client = BrowserClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location?.PathAndQuery);
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Signed_in_golfers_without_the_admin_role_are_denied(string route)
    {
        var url = Resolve(route);
        var golfer = await Data.CreateUserAsync(roles: ApplicationRoles.User);
        using var client = ClientAs(golfer.Id, ApplicationRoles.User);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/access-denied", response.Headers.Location?.PathAndQuery);
    }

    /// <remarks>
    /// The console renders interactively with prerendering off (see App.razor), so this proves the
    /// admin gets through authorization to the Blazor shell — not that the page body renders. The
    /// data each page shows comes from the admin services, which have their own tests.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Admins_are_let_through_to_the_page(string route)
    {
        var (_, client) = await SignInAdminAsync();

        var response = await client.GetAsync(Resolve(route));

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/access-denied")]
    [InlineData("/not-found")]
    public async Task Public_pages_render_for_anonymous_visitors(string route)
    {
        using var client = BrowserClient();
        await AssertStatusAsync(HttpStatusCode.OK, await client.GetAsync(route));
    }

    /// <summary>
    /// Fills route parameters with placeholder values. Authorization runs at the endpoint, before the
    /// page looks anything up, so the ids need not exist.
    /// </summary>
    private static string Resolve(string template) =>
        RouteParameter().Replace(template, m => m.Groups["constraint"].Value == ":long" ? "1" : "placeholder");

    [GeneratedRegex(@"\{(?<name>[A-Za-z]+)(?<constraint>:[^}]*)?\}")]
    private static partial Regex RouteParameter();
}
