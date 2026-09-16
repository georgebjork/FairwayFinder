using FairwayFinder.Data;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Admin.Startup;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddFairwayFinderAuthentication(this IServiceCollection services)
    {
        // Configure ASP.NET Core Identity. Same policy as the API so a password set on one
        // side is always valid on the other.
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

        // Configure Identity's application cookie. This is an admin console, so the session
        // is deliberately shorter-lived than the old public web app's.
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/authentication/logout";
            options.AccessDeniedPath = "/access-denied";
            options.Cookie.Name = ".fairway.finder.admin";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        return services;
    }
}
