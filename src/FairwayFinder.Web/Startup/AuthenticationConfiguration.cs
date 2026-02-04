using FairwayFinder.Data;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Web.Startup;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddFairwayFinderAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Sign-in settings
                options.SignIn.RequireConfirmedAccount = false;

                // Password settings
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                // User settings
                options.User.RequireUniqueEmail = true;
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Configure Identity's application cookie
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/authentication/logout";
            options.AccessDeniedPath = "/access-denied";
            options.Cookie.Name = "FairwayFinder.Auth";
        });

        services.AddAuthentication();

        return services;
    }
}