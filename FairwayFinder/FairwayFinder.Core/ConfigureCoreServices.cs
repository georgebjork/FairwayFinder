using FairwayFinder.Core.Features.UserMangement;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Core;

public static class ConfigureCoreServices
{
    public static void ConfigureServices(this IServiceCollection services) {
        
        // Repositories
        services.AddTransient<IUserRepository, UserRepository>();
        
        // Services
        services.AddTransient<IUsernameRetriever, UsernameRetriever>();
        services.AddTransient<IManageUsersService, ManageUsersService>();
    }
}