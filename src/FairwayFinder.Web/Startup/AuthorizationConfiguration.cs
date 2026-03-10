namespace FairwayFinder.Web.Startup;

public static class AuthorizationConfiguration
{
    public static IServiceCollection AddFairwayFinderAuthorization(this IServiceCollection services)
    {
        // No FallbackPolicy — authorization is enforced per-page via [Authorize] attributes
        // and AuthorizeRouteView in Routes.razor. This allows [AllowAnonymous] pages
        // (public profiles) to work with InteractiveServer mode without the auth middleware
        // blocking the SignalR circuit negotiation.
        services.AddAuthorization();
        return services;
    }
}