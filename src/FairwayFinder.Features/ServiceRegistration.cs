using FairwayFinder.Features.HttpClients;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Features.Services.TGTR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Features;

public static class ServiceRegistration
{
    public static IServiceCollection RegisterFeatureServices(this IServiceCollection services, ConfigurationManager config)
    {
        // Domain services
        services.AddTransient<IRoundService, RoundService>();
        services.AddTransient<IStatsService, StatsService>();
        services.AddTransient<ICourseService, CourseService>();

        // TGTR integration
        services.AddHttpClient<TgtrHttpClient>(client =>
        {
            var baseUrl = config["Tgtr:BaseUrl"]
                          ?? throw new InvalidOperationException("Tgtr:BaseUrl configuration is missing.");
            client.BaseAddress = new Uri(baseUrl);
        });
        services.AddTransient<TgtrTransferService>();

        return services;
    }
}
