using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Data.Migrations;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Api.IntegrationTests.Conventions;

/// <summary>
/// Runs the <c>AddUserHandle</c> migration's backfill against real rows. The column is NOT NULL now,
/// so the test relaxes it inside a transaction, nulls some handles, backfills, and rolls it all back.
/// </summary>
public class UsernameBackfillTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Backfill_gives_every_user_a_unique_username_and_the_oldest_the_clean_one()
    {
        var keeps = await Data.CreateUserAsync(firstName: "Keeps", lastName: "Hisname", username: "johnsmith");
        var olderJohn = await Data.CreateUserAsync(firstName: "John", lastName: "Smith");
        var newerJohn = await Data.CreateUserAsync(firstName: "John", lastName: "Smith");
        var olderTom = await Data.CreateUserAsync(firstName: "Tom", lastName: "Kitetest");
        var newerTom = await Data.CreateUserAsync(firstName: "Tom", lastName: "Kitetest");
        var accented = await Data.CreateUserAsync(firstName: "José", lastName: "Núñez");
        var shortName = await Data.CreateUserAsync(firstName: "Al", lastName: "");
        var reserved = await Data.CreateUserAsync(firstName: "Ad", lastName: "Min");

        var reset = new[] { olderJohn, newerJohn, olderTom, newerTom, accented, shortName, reserved }
            .Select(u => u.Id).ToList();

        await using var db = Db();
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "AspNetUsers" ALTER COLUMN "Handle" DROP NOT NULL""");
        var ids = reset.ToArray();
        await db.Database.ExecuteSqlAsync($"""UPDATE "AspNetUsers" SET "Handle" = NULL WHERE "Id" = ANY({ids})""");

        await db.Database.ExecuteSqlRawAsync(AddUserHandle.BackfillSql);

        var handles = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Handle);
        await tx.RollbackAsync();

        Assert.All(handles.Values, Assert.NotNull);
        Assert.Equal(handles.Count, handles.Values.Distinct().Count());

        Assert.Equal("johnsmith", handles[keeps.Id]);               // untouched
        Assert.Matches(@"^johnsmith\d{2,4}$", handles[olderJohn.Id]); // base taken by an existing handle
        Assert.Matches(@"^johnsmith\d{2,4}$", handles[newerJohn.Id]);
        Assert.Equal("tomkitetest", handles[olderTom.Id]);          // oldest wins the clean name
        Assert.Matches(@"^tomkitetest\d{2,4}$", handles[newerTom.Id]);
        Assert.Equal("josenunez", handles[accented.Id]);
        Assert.StartsWith("golfer", handles[shortName.Id]);
        Assert.Matches(@"^admin\d{2,4}$", handles[reserved.Id]);    // reserved names get a suffix
    }
}
