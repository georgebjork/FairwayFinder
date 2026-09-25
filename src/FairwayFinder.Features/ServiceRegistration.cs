using System.Threading.Channels;
using dotAPNS;
using FairwayFinder.Data;
using FairwayFinder.Features.HttpClients;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Services.Email;
using FairwayFinder.Features.Services.Admin;
using FairwayFinder.Features.Services.GolfCourseApi;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Features.Services.TGTR;
using FairwayFinder.Shared.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Features;

public static class ServiceRegistration
{
    /// <summary>
    /// Domain services shared by every host (the API and the admin console).
    /// Admin-only services live in <see cref="RegisterAdminServices"/> so the API does not
    /// register — or run background jobs for — things it cannot reach.
    /// </summary>
    public static IServiceCollection RegisterFeatureServices(this IServiceCollection services, ConfigurationManager config, bool isDevelopment)
    {
        // ── Data Protection ─────────────────────────────────────
        // Registered here, in the one method both hosts call, because the two must agree exactly
        // or password reset silently breaks: Identity's reset tokens are Data Protection payloads,
        // so the admin console mints a token the API has to decrypt.
        //
        // Two things have to match. PersistKeysToDbContext puts the key ring in Postgres instead of
        // each container's own filesystem, where it would be unreadable by the other host and thrown
        // away on every redeploy. SetApplicationName pins the application discriminator, which is
        // otherwise derived from the content root path and so differs between Admin and Api — that
        // discriminator is part of the purpose chain, so a mismatch fails decryption even with a
        // shared key ring. Changing this string invalidates every outstanding reset link and admin
        // auth cookie.
        services.AddDataProtection()
            .PersistKeysToDbContext<ApplicationDbContext>()
            .SetApplicationName("FairwayFinder");

        // Domain services
        services.AddTransient<IRoundService, RoundService>();
        services.AddTransient<IRoundEntryService, RoundEntryService>();
        services.AddTransient<IStatsService, StatsService>();
        services.AddTransient<ICourseService, CourseService>();
        services.AddTransient<IProfileService, ProfileService>();
        services.AddTransient<IFriendService, FriendService>();
        services.AddTransient<IGameService, GameService>();

        // The reader is the one place a game's two score sources converge. Transient like the
        // service that owns it; it holds no state between calls.
        services.AddTransient<GameScoreReader>();

        // Scoring engines are pure and stateless, so one instance each. The resolver indexes them
        // by GameType; adding a game type is a class and a line here.
        services.AddSingleton<IGameScoringEngine, MatchPlayScoringEngine>();
        services.AddSingleton<IGameScoringEngine, SkinsScoringEngine>();
        services.AddSingleton<IGameScoringEngineResolver, GameScoringEngineResolver>();

        // Invitations and request logging are used by both hosts: the API exposes invite
        // endpoints and purges request logs on a timer, the admin console manages both by hand.
        services.AddTransient<IUserInvitationService, UserInvitationService>();
        services.AddTransient<ApiRequestLogService>();

        // Password reset is reachable from both hosts: a golfer requests it from the app via the
        // API, and an admin sends the same link on their behalf from the console.
        services.AddTransient<IPasswordResetService, PasswordResetService>();

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

    /// <summary>
    /// Services only the admin console resolves: cross-user administration, the TGTR migration
    /// tool, and the GolfCourseAPI import pipeline (including its hosted background job).
    /// Requires the <c>Tgtr</c> and <c>GolfCourseApi</c> configuration sections.
    /// </summary>
    public static IServiceCollection RegisterAdminServices(this IServiceCollection services, ConfigurationManager config)
    {
        // Cross-user administration
        services.AddTransient<UserAdminService>();
        services.AddTransient<AdminDashboardService>();
        services.AddTransient<AdminRoundService>();
        services.AddTransient<AdminDeviceService>();
        services.AddTransient<AdminGameService>();

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

        return services;
    }
}
