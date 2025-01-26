using FairwayFinder.Core.Services;
using FairwayFinder.Core.UserManagement.Respositories;
using FairwayFinder.Core.UserManagement.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Core;

public static class ConfigureCoreServices
{
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        // Repositories
        services.AddTransient<IUserManagementRepository, UserManagementRepository>();
        
        // Services
        services.AddTransient<IUsernameRetriever, UsernameRetriever>();
        services.AddTransient<IUserManagementService, UserManagementService>();
        
        return services;
    } 
}