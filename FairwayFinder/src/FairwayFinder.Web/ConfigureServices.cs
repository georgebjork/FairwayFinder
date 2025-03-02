using FairwayFinder.Web.Data.Database;

namespace FairwayFinder.Web;

public static class ConfigureServices
{
    public static IServiceCollection RegisterWebServices(this IServiceCollection services)
    {
        services.AddScoped<MigrationRunner>();
        services.AddScoped<SeedAspNetIdentity>();
        return services;
    } 
}