using FairwayFinder.Core.Features.CourseManagement.Repositories;
using FairwayFinder.Core.Features.CourseManagement.Services;
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
        services.AddTransient<ICourseManagementRepository, CourseManagementRepository>();

        
        // Services
        services.AddTransient<IUsernameRetriever, UsernameRetriever>();
        services.AddTransient<IUserManagementService, UserManagementService>();
        services.AddTransient<CourseManagementService>();
        
        return services;
    } 
}