using FairwayFinder.Shared.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Shared;

public static class RegisterSettings
{
    public static IServiceCollection RegisterSharedSettings(this IServiceCollection services, ConfigurationManager config)
    {
        services.Configure<ClaudeSettings>(config.GetSection("Claude"));
        services.Configure<OpenAiSettings>(config.GetSection("OpenAI"));
        services.Configure<ResendSettings>(config.GetSection("Resend"));

        return services;
    }
}