using FairwayFinder.Web.Authorization.Profile;
using FairwayFinder.Web.Authorization.ScorecardManagement;
using FairwayFinder.Web.Data.Database;
using Microsoft.AspNetCore.Authorization;

namespace FairwayFinder.Web;

public static class ConfigureServices
{
    public static IServiceCollection RegisterWebServices(this IServiceCollection services)
    {
        services.AddScoped<MigrationRunner>();
        services.AddScoped<SeedAspNetIdentity>();
        
        
        // Auth services
        services.AddScoped<IAuthorizationHandler, CanEditScorecardHandler>();
        services.AddScoped<IAuthorizationHandler, CanEditProfileHandler>();

        
        return services;
    } 
}