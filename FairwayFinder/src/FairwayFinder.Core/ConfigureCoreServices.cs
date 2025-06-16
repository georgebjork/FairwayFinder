using FairwayFinder.Core.Authorization;
using FairwayFinder.Core.Authorization.Repositories;
using FairwayFinder.Core.Features.Dashboard.Services;
using FairwayFinder.Core.Features.GolfCourse.Repository;
using FairwayFinder.Core.Features.GolfCourse.Repository.Interfaces;
using FairwayFinder.Core.Features.GolfCourse.Services;
using FairwayFinder.Core.Features.GolfCourse.Services.Interfaces;
using FairwayFinder.Core.Features.Profile.Services;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Features.Scorecards.Services;
using FairwayFinder.Core.Features.UserManagement.Respositories;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Interfaces;
using FairwayFinder.Core.Stats.Repositories;
using FairwayFinder.Core.UserManagement.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Core;

public static class ConfigureCoreServices
{
    public static IServiceCollection RegisterCoreServices(this IServiceCollection services)
    {
        // Repositories
        services.AddTransient<IUserManagementRepository, UserManagementRepository>();
        services.AddTransient<ICourseRepository, CourseRepository>();
        services.AddTransient<ICourseStatsRepository, CourseStatsRepository>();
        services.AddTransient<ITeeboxRepository, TeeboxRepository>();
        services.AddTransient<IHoleRepository, HoleRepository>();
        services.AddTransient<IScorecardRepository, ScorecardRepository>();
        services.AddTransient<ILookupRepository, LookupRepository>();
        services.AddTransient<IAggregatedStatRepository, AggregatedStatRepository>();
        services.AddTransient<IScorecardStatRepository, ScorecardStatRepository>();
        services.AddTransient<IDocumentRepository, DocumentRepository>();
        
        // Services
        services.AddTransient<IUsernameRetriever, UsernameRetriever>();
        services.AddTransient<IUserManagementService, UserManagementService>();
        services.AddTransient<IFileUploadService, UploadThingService>();
        services.AddTransient<IProfileService, ProfileService>();
        services.AddTransient<ICourseService, CourseService>();
        services.AddTransient<ICourseStatsService, CourseStatsService>();
        services.AddTransient<ITeeboxService, TeeboxService>();
        services.AddTransient<IHoleService, HoleService>();
        services.AddTransient<IScorecardService, ScorecardService>();
        services.AddTransient<DashboardService>();
        
        
        
        // Auth Services
        services.AddTransient<ScorecardAuthorizationService>();
        
        // Auth Repositories
        services.AddTransient<IAuthorizationRepository, AuthorizationRepository>();
        
        return services;
    } 
}