using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Tests;

public class CourseServiceCascadeTests
{
    private const string UserId = "test-user";

    private static (CourseService service, InMemoryDbContextFactory factory) CreateService(string dbName)
    {
        var factory = new InMemoryDbContextFactory(dbName);
        return (new CourseService(factory), factory);
    }

    private static SaveTeeboxRequest Build(long courseId, string name, decimal rating, int slope,
        bool isWomens, int yardageBase, int handicapOffset, int? par1 = null)
    {
        var holes = Enumerable.Range(1, 18).Select(n => new HoleEntry
        {
            HoleNumber = n,
            Par = par1.HasValue && n == 1 ? par1.Value : 4,
            Yardage = yardageBase + n,
            Handicap = ((n - 1 + handicapOffset) % 18) + 1
        }).ToList();

        return new SaveTeeboxRequest
        {
            CourseId = courseId,
            TeeboxName = name,
            Rating = rating,
            Slope = slope,
            IsNineHole = false,
            IsWomens = isWomens,
            Holes = holes
        };
    }

    // Course with two men's tees (Blue, White) + one women's tee (Red), all with offset-0 handicaps.
    private static async Task<(long courseId, long blueId, long whiteId, long redId)> SeedAsync(CourseService svc)
    {
        var courseId = await svc.CreateCourseAsync(
            new SaveCourseRequest { CourseName = "Test", Address = "a", PhoneNumber = "p" }, UserId);
        var blueId = await svc.CreateTeeboxAsync(Build(courseId, "Blue", 72m, 130, false, 350, 0), UserId);
        var whiteId = await svc.CreateTeeboxAsync(Build(courseId, "White", 70m, 125, false, 320, 0), UserId);
        var redId = await svc.CreateTeeboxAsync(Build(courseId, "Red", 73m, 128, true, 300, 0), UserId);
        return (courseId, blueId, whiteId, redId);
    }

    [Fact]
    public async Task UpdateTeeboxAsync_cascade_syncs_par_handicap_to_same_gender_only()
    {
        var (svc, factory) = CreateService(nameof(UpdateTeeboxAsync_cascade_syncs_par_handicap_to_same_gender_only));
        var (courseId, blueId, whiteId, redId) = await SeedAsync(svc);

        // Re-rate Blue's handicaps (offset 5) and bump hole 1 to par 5; cascade.
        var update = Build(courseId, "Blue", 72m, 130, false, 350, handicapOffset: 5, par1: 5);
        update.TeeboxId = blueId;
        await svc.UpdateTeeboxAsync(update, UserId, cascadeToSameGender: true);

        await using var db = factory.CreateDbContext();

        // White (men's) received the new par + handicap, but kept its own yardage and rating.
        var white = await db.Teeboxes.SingleAsync(t => t.TeeboxId == whiteId);
        var whiteHole1 = await db.Holes.SingleAsync(h => h.TeeboxId == whiteId && h.HoleNumber == 1 && !h.IsDeleted);
        Assert.Equal(5, whiteHole1.Par);
        Assert.Equal(6, whiteHole1.Handicap);   // ((1-1+5)%18)+1
        Assert.Equal(321, whiteHole1.Yardage);  // White's own yardage (320+1), unchanged
        Assert.Equal(70m, white.Rating);        // unchanged

        // Red (women's) was NOT touched.
        var redHole1 = await db.Holes.SingleAsync(h => h.TeeboxId == redId && h.HoleNumber == 1 && !h.IsDeleted);
        Assert.Equal(4, redHole1.Par);
        Assert.Equal(1, redHole1.Handicap);
    }

    [Fact]
    public async Task UpdateTeeboxAsync_without_cascade_leaves_other_tees_untouched()
    {
        var (svc, factory) = CreateService(nameof(UpdateTeeboxAsync_without_cascade_leaves_other_tees_untouched));
        var (courseId, blueId, whiteId, _) = await SeedAsync(svc);

        var update = Build(courseId, "Blue", 72m, 130, false, 350, handicapOffset: 5, par1: 5);
        update.TeeboxId = blueId;
        await svc.UpdateTeeboxAsync(update, UserId, cascadeToSameGender: false);

        await using var db = factory.CreateDbContext();
        var whiteHole1 = await db.Holes.SingleAsync(h => h.TeeboxId == whiteId && h.HoleNumber == 1 && !h.IsDeleted);
        Assert.Equal(4, whiteHole1.Par);      // unchanged
        Assert.Equal(1, whiteHole1.Handicap); // unchanged
    }

    [Fact]
    public async Task CreateTeeboxVersionAsync_cascade_versions_all_same_gender_tees()
    {
        var (svc, factory) = CreateService(nameof(CreateTeeboxVersionAsync_cascade_versions_all_same_gender_tees));
        var (courseId, blueId, whiteId, redId) = await SeedAsync(svc);

        var update = Build(courseId, "Blue", 75m, 140, false, 350, handicapOffset: 5, par1: 5);
        update.TeeboxId = blueId;
        var newBlueId = await svc.CreateTeeboxVersionAsync(update, UserId, cascadeToSameGender: true);

        await using var db = factory.CreateDbContext();

        // Blue: old archived, new active.
        Assert.NotNull((await db.Teeboxes.SingleAsync(t => t.TeeboxId == blueId)).ArchivedOn);
        Assert.Null((await db.Teeboxes.SingleAsync(t => t.TeeboxId == newBlueId)).ArchivedOn);

        // White: old archived; a new active version exists in the same lineage with cascaded
        // par/handicap but White's own rating + yardage.
        var oldWhite = await db.Teeboxes.SingleAsync(t => t.TeeboxId == whiteId);
        Assert.NotNull(oldWhite.ArchivedOn);
        var newWhite = await db.Teeboxes.SingleAsync(t =>
            t.TeeboxGroupId == oldWhite.TeeboxGroupId && t.ArchivedOn == null && !t.IsDeleted);
        Assert.NotEqual(whiteId, newWhite.TeeboxId);
        Assert.Equal(70m, newWhite.Rating);

        var newWhiteHole1 = await db.Holes.SingleAsync(h => h.TeeboxId == newWhite.TeeboxId && h.HoleNumber == 1 && !h.IsDeleted);
        Assert.Equal(5, newWhiteHole1.Par);       // cascaded
        Assert.Equal(6, newWhiteHole1.Handicap);  // cascaded
        Assert.Equal(321, newWhiteHole1.Yardage); // White's own yardage preserved

        // Red (women's) untouched — still the only women's tee, not archived.
        Assert.Null((await db.Teeboxes.SingleAsync(t => t.TeeboxId == redId)).ArchivedOn);
        Assert.Equal(1, await db.Teeboxes.CountAsync(t => t.IsWomens && !t.IsDeleted && t.ArchivedOn == null));
    }

    [Fact]
    public async Task GetParHandicapTemplateAsync_returns_same_gender_holes()
    {
        var (svc, _) = CreateService(nameof(GetParHandicapTemplateAsync_returns_same_gender_holes));
        var (courseId, _, _, _) = await SeedAsync(svc);

        var mens = await svc.GetParHandicapTemplateAsync(courseId, isWomens: false, isNineHole: false);
        Assert.Equal(18, mens.Count);
        Assert.Equal(1, mens.Single(h => h.HoleNumber == 1).Handicap);
        Assert.All(mens, h => Assert.Equal(4, h.Par));

        var womens = await svc.GetParHandicapTemplateAsync(courseId, isWomens: true, isNineHole: false);
        Assert.Equal(18, womens.Count);
    }

    [Fact]
    public async Task GetParHandicapTemplateAsync_returns_empty_when_no_tees()
    {
        var (svc, _) = CreateService(nameof(GetParHandicapTemplateAsync_returns_empty_when_no_tees));
        var courseId = await svc.CreateCourseAsync(
            new SaveCourseRequest { CourseName = "Empty", Address = "a", PhoneNumber = "p" }, UserId);

        var template = await svc.GetParHandicapTemplateAsync(courseId, isWomens: false, isNineHole: false);
        Assert.Empty(template);
    }
}
