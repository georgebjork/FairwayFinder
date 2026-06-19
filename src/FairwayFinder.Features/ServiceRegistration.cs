using System.Threading.Channels;
using dotAPNS;
using FairwayFinder.Features.HttpClients;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Services.Email;
using FairwayFinder.Features.Services.Admin;
using FairwayFinder.Features.Services.GolfCourseApi;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Features.Services.TGTR;
using FairwayFinder.Shared.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Features;

public static class ServiceRegistration
{
    public static IServiceCollection RegisterFeatureServices(this IServiceCollection services, ConfigurationManager config, bool isDevelopment)
    {
        // Admin services
        services.AddTransient<UserAdminService>();
        services.AddTransient<IUserInvitationService, UserInvitationService>();

        // Domain services
        services.AddTransient<IRoundService, RoundService>();
        services.AddTransient<IStatsService, StatsService>();
        services.AddTransient<ICourseService, CourseService>();
        services.AddTransient<IProfileService, ProfileService>();
        services.AddTransient<IFriendService, FriendService>();

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
        services.AddSingleton(Channel.CreateBounded<int>(1));
        services.AddSingleton<GolfCourseApiImportState>();
        services.AddSingleton<GolfCourseApiImportJob>();
        services.AddHostedService(sp => sp.GetRequiredService<GolfCourseApiImportJob>());

        // APNS push notifications
        services.AddHttpClient("apns");
        services.AddSingleton<IApnsClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<ApnsSettings>>().Value;
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var jwtOptions = new ApnsJwtOptions
            {
                BundleId = settings.BundleId,
                CertContent = settings.P8Contents,
                KeyId = settings.KeyId,
                TeamId = settings.TeamId
            };
            return ApnsClient.CreateUsingJwt(httpFactory.CreateClient("apns"), jwtOptions);
        });
        services.AddScoped<IPushNotificationService, PushNotificationService>();

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
