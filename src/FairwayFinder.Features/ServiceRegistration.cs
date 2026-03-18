using System.Threading.Channels;
using FairwayFinder.Features.HttpClients;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Services.Email;
using FairwayFinder.Features.Services.Admin;
using FairwayFinder.Features.Services.GolfCourseApi;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Features.Services.TGTR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Features;

public static class ServiceRegistration
{
    public static IServiceCollection RegisterFeatureServices(this IServiceCollection services, ConfigurationManager config, bool isDevelopment)
    {
        // Admin services
        services.AddTransient<UserAdminService>();

        // Domain services
        services.AddTransient<IRoundService, RoundService>();
        services.AddTransient<IStatsService, StatsService>();
        services.AddTransient<ICourseService, CourseService>();
        services.AddTransient<IProfileService, ProfileService>();

        // TGTR integration
        services.AddHttpClient<TgtrHttpClient>(client =>
        {
            var baseUrl = config["Tgtr:BaseUrl"]
                          ?? throw new InvalidOperationException("Tgtr:BaseUrl configuration is missing.");
            client.BaseAddress = new Uri(baseUrl);
        });
        services.AddTransient<TgtrTransferService>();

        // GolfCourseAPI integration
        services.AddHttpClient<GolfCourseApiHttpClient>(client =>
        {
            var baseUrl = config["GolfCourseApi:BaseUrl"]
                          ?? throw new InvalidOperationException("GolfCourseApi:BaseUrl configuration is missing.");
            var apiKey = config["GolfCourseApi:ApiKey"]
                         ?? throw new InvalidOperationException("GolfCourseApi:ApiKey configuration is missing.");
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Authorization", $"Key {apiKey}");
        });
        services.AddTransient<GolfCourseApiImportService>();
        services.AddSingleton(Channel.CreateBounded<bool>(1));
        services.AddSingleton<GolfCourseApiImportState>();
        services.AddSingleton<GolfCourseApiImportJob>();
        services.AddHostedService(sp => sp.GetRequiredService<GolfCourseApiImportJob>());

        // Email — use dev sender locally to avoid sending real emails
        if (isDevelopment)
        {
            services.AddTransient<IEmailSender, DevEmailSender>();
        }
        else
        {
            services.AddTransient<IEmailSender, ResendEmailSender>();
        }

        return services;
    }
}
