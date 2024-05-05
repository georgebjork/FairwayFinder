using FairwayFinder.Identity.Authorization;
using FairwayFinder.Identity.Authorization.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Identity;

public static class ConfigureIdentityServices
{
    public static void ConfigureServices(this IServiceCollection services)
    {
        services.AddTransient<IUserRefreshService, UserRefreshService>();
        
        // Profile auth
        services.AddSingleton<IAuthorizationHandler, EditProfileAuthorization>();
    }
}