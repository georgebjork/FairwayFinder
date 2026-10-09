using FairwayFinder.Api.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Api.IntegrationTests.Conventions;

/// <summary>Keeps the EF model and the committed migrations in step.</summary>
public class MigrationTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Every_migration_applies_cleanly_to_an_empty_postgres()
    {
        await using var db = Db();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public void Model_has_no_changes_missing_a_migration()
    {
        // Fails when an entity or configuration changed without `dotnet ef migrations add`.
        using var db = Db();
        Assert.False(db.Database.HasPendingModelChanges(),
            "The EF model has changes that are not captured in a migration. Run `dotnet ef migrations add <Name> " +
            "--project src/FairwayFinder.Data --startup-project src/FairwayFinder.Admin`.");
    }
}
