using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Tests;

public class CourseServiceTeeboxVersioningTests
{
    private const string UserId = "test-user";

    private static (CourseService service, InMemoryDbContextFactory factory) CreateService(string dbName)
    {
        var factory = new InMemoryDbContextFactory(dbName);
        return (new CourseService(factory), factory);
    }

    private static SaveTeeboxRequest BuildTeeboxRequest(long courseId, string name, decimal rating, int slope, int handicapOffset = 0)
    {
        // 18 holes with unique handicaps; handicapOffset lets a new version shuffle handicaps.
        var holes = Enumerable.Range(1, 18).Select(n => new HoleEntry
        {
            HoleNumber = n,
            Par = 4,
            Yardage = 350 + n,
            Handicap = ((n - 1 + handicapOffset) % 18) + 1
        }).ToList();

        return new SaveTeeboxRequest
        {
            CourseId = courseId,
            TeeboxName = name,
            Rating = rating,
            Slope = slope,
            IsNineHole = false,
            IsWomens = false,
            Holes = holes
        };
    }

    private static async Task<long> SeedCourseWithTeeboxAsync(CourseService service, SaveTeeboxRequest teebox)
    {
        var courseId = await service.CreateCourseAsync(
            new SaveCourseRequest { CourseName = "Test Course", Address = "123 Fairway", PhoneNumber = "555-0100" },
            UserId);
        teebox.CourseId = courseId;
        var teeboxId = await service.CreateTeeboxAsync(teebox, UserId);
        return teeboxId;
    }

    [Fact]
    public async Task CreateTeeboxAsync_sets_group_id_to_its_own_id()
    {
        var (service, factory) = CreateService(nameof(CreateTeeboxAsync_sets_group_id_to_its_own_id));

        var teeboxId = await SeedCourseWithTeeboxAsync(service, BuildTeeboxRequest(0, "Blue", 72.1m, 130));

        await using var db = factory.CreateDbContext();
        var teebox = await db.Teeboxes.SingleAsync(t => t.TeeboxId == teeboxId);
        Assert.Equal(teebox.TeeboxId, teebox.TeeboxGroupId);
        Assert.Null(teebox.ArchivedOn);
    }

    [Fact]
    public async Task CreateTeeboxVersionAsync_archives_source_and_clones_into_same_group()
    {
        var (service, factory) = CreateService(nameof(CreateTeeboxVersionAsync_archives_source_and_clones_into_same_group));

        var sourceId = await SeedCourseWithTeeboxAsync(service, BuildTeeboxRequest(0, "Blue", 72.1m, 130));
        long courseId;
        long sourceGroupId;
        await using (var setup = factory.CreateDbContext())
        {
            var src = await setup.Teeboxes.SingleAsync(t => t.TeeboxId == sourceId);
            courseId = src.CourseId;
            sourceGroupId = src.TeeboxGroupId;
        }

        // Re-rate: new slope/rating and shuffled handicaps
        var versionRequest = BuildTeeboxRequest(courseId, "Blue", 73.4m, 138, handicapOffset: 3);
        versionRequest.TeeboxId = sourceId;
        var newId = await service.CreateTeeboxVersionAsync(versionRequest, UserId);

        Assert.True(newId > 0);
        Assert.NotEqual(sourceId, newId);

        await using var db = factory.CreateDbContext();
        var source = await db.Teeboxes.SingleAsync(t => t.TeeboxId == sourceId);
        var version = await db.Teeboxes.SingleAsync(t => t.TeeboxId == newId);

        // Source archived but not deleted; its holes untouched (still 18 active)
        Assert.NotNull(source.ArchivedOn);
        Assert.Equal(UserId, source.ArchivedBy);
        Assert.False(source.IsDeleted);
        Assert.Equal(72.1m, source.Rating);
        Assert.Equal(18, await db.Holes.CountAsync(h => h.TeeboxId == sourceId && !h.IsDeleted));

        // New version active, same lineage, new values, own cloned holes
        Assert.Null(version.ArchivedOn);
        Assert.Equal(sourceGroupId, version.TeeboxGroupId);
        Assert.Equal(73.4m, version.Rating);
        Assert.Equal(138, version.Slope);
        Assert.Equal(18, await db.Holes.CountAsync(h => h.TeeboxId == newId && !h.IsDeleted));
    }

    [Fact]
    public async Task CreateTeeboxVersionAsync_rejects_already_archived_source()
    {
        var (service, factory) = CreateService(nameof(CreateTeeboxVersionAsync_rejects_already_archived_source));

        var sourceId = await SeedCourseWithTeeboxAsync(service, BuildTeeboxRequest(0, "Blue", 72.1m, 130));
        long courseId;
        await using (var setup = factory.CreateDbContext())
        {
            courseId = (await setup.Teeboxes.SingleAsync(t => t.TeeboxId == sourceId)).CourseId;
        }

        var firstVersion = BuildTeeboxRequest(courseId, "Blue", 73.0m, 135);
        firstVersion.TeeboxId = sourceId;
        await service.CreateTeeboxVersionAsync(firstVersion, UserId); // archives sourceId

        // Versioning the now-archived source must be rejected
        var secondVersion = BuildTeeboxRequest(courseId, "Blue", 74.0m, 140);
        secondVersion.TeeboxId = sourceId;
        var result = await service.CreateTeeboxVersionAsync(secondVersion, UserId);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetTeeboxesAsync_excludes_archived_versions()
    {
        var (service, factory) = CreateService(nameof(GetTeeboxesAsync_excludes_archived_versions));

        var sourceId = await SeedCourseWithTeeboxAsync(service, BuildTeeboxRequest(0, "Blue", 72.1m, 130));
        long courseId;
        await using (var setup = factory.CreateDbContext())
        {
            courseId = (await setup.Teeboxes.SingleAsync(t => t.TeeboxId == sourceId)).CourseId;
        }

        var versionRequest = BuildTeeboxRequest(courseId, "Blue", 73.4m, 138);
        versionRequest.TeeboxId = sourceId;
        var newId = await service.CreateTeeboxVersionAsync(versionRequest, UserId);

        var pickerOptions = await service.GetTeeboxesAsync(courseId);

        Assert.Single(pickerOptions);
        Assert.Equal(newId, pickerOptions[0].TeeboxId);
    }

    [Fact]
    public async Task GetCourseDetailAsync_returns_archived_version_with_active_first()
    {
        var (service, factory) = CreateService(nameof(GetCourseDetailAsync_returns_archived_version_with_active_first));

        var sourceId = await SeedCourseWithTeeboxAsync(service, BuildTeeboxRequest(0, "Blue", 72.1m, 130));
        long courseId;
        await using (var setup = factory.CreateDbContext())
        {
            courseId = (await setup.Teeboxes.SingleAsync(t => t.TeeboxId == sourceId)).CourseId;
        }

        var versionRequest = BuildTeeboxRequest(courseId, "Blue", 73.4m, 138);
        versionRequest.TeeboxId = sourceId;
        var newId = await service.CreateTeeboxVersionAsync(versionRequest, UserId);

        var detail = await service.GetCourseDetailAsync(courseId);

        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Teeboxes.Count);
        // Active version listed before the archived one
        Assert.Equal(newId, detail.Teeboxes[0].TeeboxId);
        Assert.Null(detail.Teeboxes[0].ArchivedOn);
        Assert.Equal(sourceId, detail.Teeboxes[1].TeeboxId);
        Assert.NotNull(detail.Teeboxes[1].ArchivedOn);
    }
}
