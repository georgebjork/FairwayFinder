using FairwayFinder.Core.Features.Admin.UserManagement;
using FairwayFinder.Core.Features.CourseManagement.Repository;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Features.Profile;
using FairwayFinder.Core.Features.Profile.Services;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Core;

public static class ConfigureCoreServices
{
    public static void ConfigureServices(this IServiceCollection services) {
        
        // Repositories
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IProfileRepository, ProfileRepository>();
        services.AddTransient<ICourseRepository, CourseRepository>();
        
        // Services
        services.AddTransient<IUsernameRetriever, UsernameRetriever>();
        services.AddTransient<IManageUsersService, ManageUsersService>();
        services.AddTransient<IEmailSenderService, SendGridEmailService>();
        services.AddTransient<IMyProfileService, MyProfileService>();
        services.AddTransient<IProfileService, ProfileService>();
        services.AddTransient<ICourseManagementService, CourseManagementService>();
        
        
        services.AddMediatR(x=> x.RegisterServicesFromAssemblyContaining<ICoreAssemblyMarker>());

    }
}

public interface ICoreAssemblyMarker
{
    
}