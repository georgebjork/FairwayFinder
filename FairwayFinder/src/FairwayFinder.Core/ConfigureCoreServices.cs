using FairwayFinder.Core.Features.CourseManagement.Repositories;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Features.Dashboard.Services;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Stats.Repositories;
using FairwayFinder.Core.UserManagement.Respositories;
using FairwayFinder.Core.UserManagement.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Core;

public static class ConfigureCoreServices
{
    public static IServiceCollection RegisterCoreServices(this IServiceCollection services)
    {
        // Repositories
        services.AddTransient<IUserManagementRepository, UserManagementRepository>();
        services.AddTransient<ICourseManagementRepository, CourseManagementRepository>();
        services.AddTransient<ICourseLookupRepository, CourseLookupRepository>();
        services.AddTransient<ITeeboxLookupRepository, TeeboxLookupRepository>();
        services.AddTransient<IHoleLookupRepository, HoleLookupRepository>();
        services.AddTransient<IScorecardRepository, ScorecardRepository>();
        services.AddTransient<IScorecardManagementRepository, ScorecardManagementRepository>();
        services.AddTransient<ILookupRepository, LookupRepository>();
        services.AddTransient<IStatRepository, StatRepository>();

        
        // Services
        services.AddTransient<IUsernameRetriever, UsernameRetriever>();
        services.AddTransient<IUserManagementService, UserManagementService>();
        services.AddTransient<CourseManagementService>();
        services.AddTransient<CourseLookupService>();
        services.AddTransient<TeeboxLookupService>();
        services.AddTransient<HoleLookupService>();
        services.AddTransient<ScorecardService>();
        services.AddTransient<ScorecardManagementService>();
        services.AddTransient<DashboardService>();
        
        return services;
    } 
}