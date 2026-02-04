using Microsoft.AspNetCore.Authorization;

namespace FairwayFinder.Web.Startup;

public static class AuthorizationConfiguration
{
    public static IServiceCollection AddFairwayFinderAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        return services;
    }
}