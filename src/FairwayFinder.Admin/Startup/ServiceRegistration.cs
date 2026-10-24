using FairwayFinder.Admin.Components.Auth;
using FairwayFinder.Admin.Components.Shared.Layout.Breadcrumb;
using FairwayFinder.Admin.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace FairwayFinder.Admin.Startup;

public static class ServiceRegistration
{
    public static IServiceCollection RegisterAdminWebServices(this IServiceCollection services)
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
        services.AddScoped<CircuitHandler, LogContextCircuitHandler>();

        // Retention for the Warning+ application log. Admin owns the logs database's lifecycle
        // (it migrates it), so the purge runs here.
        services.AddHostedService<AppLogPurgeService>();
        services.AddHttpContextAccessor();

        return services;
    }
}
