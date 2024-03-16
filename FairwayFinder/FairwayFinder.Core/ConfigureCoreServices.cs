using FairwayFinder.Core.Features.Profile;
using FairwayFinder.Core.Features.Profile.Services;
using FairwayFinder.Core.Features.UserMangement;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Core;

public static class ConfigureCoreServices
{
    public static void ConfigureServices(this IServiceCollection services) {
        
        // Repositories
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IProfileRepository, ProfileRepository>();
        
        // Services
        services.AddTransient<IUsernameRetriever, UsernameRetriever>();
        services.AddTransient<IManageUsersService, ManageUsersService>();
        services.AddTransient<IEmailSenderService, SendGridEmailService>();
        services.AddTransient<IMyProfileService, MyProfileService>();
        services.AddTransient<IProfileService, ProfileService>();
    }
}