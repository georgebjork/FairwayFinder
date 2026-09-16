using FairwayFinder.Identity;

namespace FairwayFinder.Admin.Startup;

/// <summary>
/// Policy names used by <c>[Authorize(Policy = ...)]</c> across the admin app.
/// </summary>
public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
}

public static class AuthorizationConfiguration
{
    public static IServiceCollection AddFairwayFinderAuthorization(this IServiceCollection services)
    {
        // No FallbackPolicy — authorization is enforced per-page via [Authorize(Policy = Policies.AdminOnly)]
        // and AuthorizeRouteView in Routes.razor. A fallback policy would block the SignalR circuit
        // negotiation that InteractiveServer pages depend on.
        services.AddAuthorization(options =>
            options.AddPolicy(Policies.AdminOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(ApplicationRoles.Admin)));

        return services;
    }
}
