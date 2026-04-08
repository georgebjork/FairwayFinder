using FairwayFinder.Data;
using FairwayFinder.Features.Enums;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Api.Endpoints;

public static class LookupEndpoints
{
    public static WebApplication MapLookupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/lookups")
            .WithTags("Lookups")
            .RequireAuthorization();

        group.MapGet("/miss-types", async (IDbContextFactory<ApplicationDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var missTypes = await db.MissTypes.Select(m => new { m.MissTypeId, m.MissType1 }).ToListAsync();
            return Results.Ok(missTypes);
        });

        group.MapGet("/lie-types", () =>
        {
            var lieTypes = Enum.GetValues<LieType>()
                .Select(l => new { Value = (int)l, Name = l.ToString() });
            return Results.Ok(lieTypes);
        });

        group.MapGet("/distance-units", () =>
        {
            var units = new[]
            {
                new { Value = DistanceUnit.Yards, Name = "Yards" },
                new { Value = DistanceUnit.Feet, Name = "Feet" }
            };
            return Results.Ok(units);
        });

        return app;
    }
}
