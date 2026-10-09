using FairwayFinder.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Api.IntegrationTests.Conventions;

/// <summary>
/// Guards the API's auth surface as it grows. Every endpoint the host maps must either require
/// authorization or be on the explicit anonymous list below — so a new endpoint that forgets
/// <c>.RequireAuthorization()</c> fails here instead of shipping open.
/// </summary>
/// <remarks>
/// If this fails because you added a genuinely public endpoint, add it to
/// <see cref="AnonymousEndpoints"/> and add a test for it in <c>PublicEndpointsTests</c> or
/// <c>AuthEndpointsTests</c>.
/// </remarks>
public class EndpointAuthorizationConventionTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly HashSet<string> AnonymousEndpoints =
    [
        "POST /api/auth/login",
        "POST /api/auth/refresh",
        "POST /api/auth/logout",
        "POST /api/auth/forgot-password",
        "POST /api/auth/reset-password",
        "POST /api/auth/register",
        "GET /api/auth/invites/{code}",
        "GET /.well-known/apple-app-site-association",
        "GET /register",
        "GET /reset-password",
    ];

    /// <summary>Endpoints restricted to the Admin role. Anything here must not be reachable by a plain golfer.</summary>
    private static readonly HashSet<string> AdminOnlyEndpoints =
    [
        "GET /api/admin/invites/",
        "POST /api/admin/invites/",
        "DELETE /api/admin/invites/{id:int}",
        "POST /api/devices/test",
    ];

    private IReadOnlyList<(string Key, Endpoint Endpoint)> MappedEndpoints() =>
        Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => ($"{method} {e.RoutePattern.RawText}", (Endpoint)e)))
            .ToList();

    [Fact]
    public void Every_endpoint_requires_authorization_unless_explicitly_public()
    {
        var violations = new List<string>();

        foreach (var (key, endpoint) in MappedEndpoints())
        {
            var allowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var requiresAuth = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any()
                               || endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Any();

            if (AnonymousEndpoints.Contains(key))
            {
                if (!allowsAnonymous)
                    violations.Add($"{key} is listed as public but is not marked AllowAnonymous.");
            }
            else if (allowsAnonymous || !requiresAuth)
            {
                violations.Add($"{key} is reachable without authorization.");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Admin_endpoints_require_the_admin_role()
    {
        var endpoints = MappedEndpoints().ToDictionary(e => e.Key, e => e.Endpoint);
        var violations = new List<string>();

        foreach (var key in AdminOnlyEndpoints)
        {
            if (!endpoints.TryGetValue(key, out var endpoint))
            {
                violations.Add($"{key} is listed as admin-only but is no longer mapped.");
                continue;
            }

            var roles = endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()
                .SelectMany(p => p.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>())
                .SelectMany(r => r.AllowedRoles)
                .Concat(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                    .SelectMany(a => a.Roles?.Split(',') ?? []));

            if (!roles.Contains(FairwayFinder.Identity.ApplicationRoles.Admin))
                violations.Add($"{key} does not require the Admin role.");
        }

        // Anything under /api/admin must be on the admin list.
        violations.AddRange(endpoints.Keys
            .Where(k => k.Contains(" /api/admin/") && !AdminOnlyEndpoints.Contains(k))
            .Select(k => $"{k} is under /api/admin but not in AdminOnlyEndpoints."));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Every_public_endpoint_on_the_list_is_still_mapped()
    {
        var mapped = MappedEndpoints().Select(e => e.Key).ToHashSet();
        var missing = AnonymousEndpoints.Where(k => !mapped.Contains(k)).ToList();
        Assert.True(missing.Count == 0, $"Listed as public but no longer mapped: {string.Join(", ", missing)}");
    }
}
