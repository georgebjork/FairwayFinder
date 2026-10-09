using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Admin.Services;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Admin.IntegrationTests.Startup;

/// <summary>
/// The admin console provisions the whole system at boot: schema, roles, and the first admin.
/// The host under test has already been through that path against an empty database.
/// </summary>
public class AdminStartupTests(AdminFactory factory) : AdminTestBase(factory)
{
    [Fact]
    public async Task Startup_applies_every_migration()
    {
        await using var db = Db();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Startup_creates_the_admin_and_user_roles()
    {
        await using var scope = Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        Assert.True(await roles.RoleExistsAsync(ApplicationRoles.Admin));
        Assert.True(await roles.RoleExistsAsync(ApplicationRoles.User));
    }

    [Fact]
    public async Task Startup_provisioning_is_idempotent_and_seeds_the_configured_admin()
    {
        // The per-test reset wiped the user created at boot; run the startup steps again, twice,
        // the way every redeploy does.
        await using var scope = Services.CreateAsyncScope();
        var startup = scope.ServiceProvider.GetRequiredService<IApplicationStartupService>();
        for (var i = 0; i < 2; i++)
        {
            await startup.RunMigrationsAsync();
            await startup.EnsureRolesExistAsync();
            await startup.SeedDefaultUserAsync();
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var seeded = await users.FindByEmailAsync(AdminFactory.SeedAdminEmail);
        Assert.NotNull(seeded);
        Assert.True(seeded.EmailConfirmed);
        Assert.True(await users.IsInRoleAsync(seeded, ApplicationRoles.Admin));
        Assert.True(await users.CheckPasswordAsync(seeded, AdminFactory.SeedAdminPassword));

        await using var db = Db();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == AdminFactory.SeedAdminEmail));
        Assert.True(await db.UserProfiles.AnyAsync(p => p.UserId == seeded.Id));
    }
}
