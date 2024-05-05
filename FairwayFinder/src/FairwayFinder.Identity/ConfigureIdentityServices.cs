using FairwayFinder.Identity.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Identity;

public static class ConfigureIdentityServices
{
    public static void ConfigureServices(this IServiceCollection services)
    {
        services.AddTransient<IUserRefreshService, UserRefreshService>();
    }
}