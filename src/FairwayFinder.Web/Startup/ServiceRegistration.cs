using FairwayFinder.Web.Components.Auth;
using FairwayFinder.Web.Components.Shared.Layout;
using FairwayFinder.Web.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace FairwayFinder.Web.Startup;

public static class ServiceRegistration
{
    public static IServiceCollection RegisterWebServices(this IServiceCollection services)
    {
        // Auth
        services.AddScoped<IdentityRedirectManager>();

        // UI state
        services.AddScoped<BreadcrumbState>();

        // App services
        services.AddTransient<IApplicationStartupService, ApplicationStartupService>();

        // Circuit tracking (singleton so state is shared across all connections)
        services.AddSingleton<CircuitTrackingService>();
        services.AddScoped<CircuitHandler, TrackingCircuitHandler>();
        services.AddHttpContextAccessor();

        return services;
    }
}
