using FairwayFinder.Api.BackgroundServices;
using FairwayFinder.Data;
using FairwayFinder.Identity;
using FairwayFinder.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FairwayFinder.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The real API host — JWT auth, validation filters, exception handler, every endpoint — over a
/// throwaway Postgres.
/// </summary>
public sealed class ApiFactory : FairwayFinderFactory<Program>
{
    /// <summary>
    /// The API assumes the schema exists (the admin console owns migrations and roles in every
    /// real environment), so the test host does that admin work itself before the first test.
    /// </summary>
    protected override async Task PrepareDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();

        var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { ApplicationRoles.Admin, ApplicationRoles.User })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // The request-log writer and purge timer would race the per-test reset. Request logging
        // is switched off in TestSettings; the purge is exercised directly in its own test.
        var hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService)
                        && (d.ImplementationType == typeof(RequestLogWriter)
                            || d.ImplementationType == typeof(RequestLogPurgeService)))
            .ToList();

        foreach (var descriptor in hostedServices)
            services.Remove(descriptor);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
