using FairwayFinder.Web.Components.Auth;
using FairwayFinder.Web.Components.Shared.Layout;
using FairwayFinder.Web.Services;

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
        services.AddTransient<IApplicationRoleService, ApplicationRoleService>();

        return services;
    }
}
