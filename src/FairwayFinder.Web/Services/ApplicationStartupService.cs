using FairwayFinder.Data;
using FairwayFinder.Identity;
using FairwayFinder.Shared.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Web.Services;

public interface IApplicationStartupService
{
    Task RunMigrationsAsync();
    Task EnsureRolesExistAsync();
    Task SeedDefaultUserAsync();
}

public class ApplicationStartupService : IApplicationStartupService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SeedUserSettings _seedUserSettings;
    private readonly ILogger<ApplicationStartupService> _logger;

    public ApplicationStartupService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<SeedUserSettings> seedUserSettings,
        ILogger<ApplicationStartupService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _roleManager = roleManager;
        _userManager = userManager;
        _seedUserSettings = seedUserSettings.Value;
        _logger = logger;
    }

    public async Task RunMigrationsAsync()
    {
        _logger.LogInformation("Running database migrations...");
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();
        _logger.LogInformation("Database migrations completed.");
    }

    public async Task EnsureRolesExistAsync()
    {
        var roles = new[] { ApplicationRoles.Admin, ApplicationRoles.User };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                _logger.LogInformation("Creating application role: {Role}", role);
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    public async Task SeedDefaultUserAsync()
    {
        if (string.IsNullOrWhiteSpace(_seedUserSettings.Email) ||
            string.IsNullOrWhiteSpace(_seedUserSettings.Password))
        {
            _logger.LogInformation("Seed user settings not configured. Skipping default user creation.");
            return;
        }

        var existingUser = await _userManager.FindByEmailAsync(_seedUserSettings.Email);
        if (existingUser is not null)
        {
            _logger.LogInformation("Seed user {Email} already exists. Skipping.", _seedUserSettings.Email);
            return;
        }

        var user = new ApplicationUser
        {
            FirstName = _seedUserSettings.FirstName,
            LastName = _seedUserSettings.LastName,
            EmailConfirmed = true
        };

        await _userManager.SetUserNameAsync(user, _seedUserSettings.Email);
        await _userManager.SetEmailAsync(user, _seedUserSettings.Email);

        var result = await _userManager.CreateAsync(user, _seedUserSettings.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create seed user {Email}: {Errors}", _seedUserSettings.Email, errors);
            return;
        }

        await _userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
        _logger.LogInformation("Seed user {Email} created with Admin role.", _seedUserSettings.Email);
    }
}
