using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// Covers the ExcludeFromStats flag: who is allowed to set it, and the split between round
/// lists (which keep showing excluded rounds so they can be un-excluded) and the stats/details
/// path (which never returns them).
/// </summary>
public class RoundServiceExcludeFromStatsTests
{
    private const string OwnerId = "owner-user";
    private const string OtherUserId = "other-user";

    private static (RoundService service, InMemoryDbContextFactory factory) CreateService(string dbName)
    {
        var factory = new InMemoryDbContextFactory(dbName);
        var service = new RoundService(
            factory,
            new ThrowingFriendService(),
            new NoOpPushNotificationService(),
            NullLogger<RoundService>.Instance);

        return (service, factory);
    }

    /// <summary>
    /// Seeds one course + teebox and a round per entry in <paramref name="excludeFlags"/>,
    /// returning the new round ids in the same order.
    /// </summary>
    private static async Task<List<long>> SeedRoundsAsync(InMemoryDbContextFactory factory, params bool[] excludeFlags)
    {
        await using var db = factory.CreateDbContext();

        var course = new Course
        {
            CourseName = "Test Course",
            CreatedBy = OwnerId,
            UpdatedBy = OwnerId
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var teebox = new Teebox
        {
            CourseId = course.CourseId,
            TeeboxName = "Blue",
            Par = 72,
            Rating = 71.5m,
            Slope = 130,
            CreatedBy = OwnerId,
            UpdatedBy = OwnerId
        };
        db.Teeboxes.Add(teebox);
        await db.SaveChangesAsync();

        var rounds = excludeFlags.Select((exclude, i) => new Round
        {
            CourseId = course.CourseId,
            TeeboxId = teebox.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1).AddDays(i),
            UserId = OwnerId,
            CreatedBy = OwnerId,
            UpdatedBy = OwnerId,
            Score = 82,
            FullRound = true,
            // These tests are about the ExcludeFromStats split, so every seeded round is a
            // posted one — an incomplete round is filtered out before that flag is consulted.
            IsComplete = true,
            ExcludeFromStats = exclude
        }).ToList();

        db.Rounds.AddRange(rounds);
        await db.SaveChangesAsync();

        return rounds.Select(r => r.RoundId).ToList();
    }

    private static async Task<bool> ReadExcludeFlagAsync(InMemoryDbContextFactory factory, long roundId)
    {
        await using var db = factory.CreateDbContext();
        var round = await db.Rounds.SingleAsync(r => r.RoundId == roundId);
        return round.ExcludeFromStats;
    }

    [Fact]
    public async Task SetExcludeFromStatsAsync_owner_can_exclude_and_re_include()
    {
        var (service, factory) = CreateService(nameof(SetExcludeFromStatsAsync_owner_can_exclude_and_re_include));
        var roundId = (await SeedRoundsAsync(factory, false)).Single();

        var excluded = await service.SetExcludeFromStatsAsync(roundId, exclude: true, OwnerId);

        Assert.True(excluded);
        Assert.True(await ReadExcludeFlagAsync(factory, roundId));

        var reIncluded = await service.SetExcludeFromStatsAsync(roundId, exclude: false, OwnerId);

        Assert.True(reIncluded);
        Assert.False(await ReadExcludeFlagAsync(factory, roundId));
    }

    [Fact]
    public async Task SetExcludeFromStatsAsync_rejects_a_user_who_does_not_own_the_round()
    {
        var (service, factory) = CreateService(nameof(SetExcludeFromStatsAsync_rejects_a_user_who_does_not_own_the_round));
        var roundId = (await SeedRoundsAsync(factory, false)).Single();

        var result = await service.SetExcludeFromStatsAsync(roundId, exclude: true, OtherUserId);

        Assert.False(result);
        Assert.False(await ReadExcludeFlagAsync(factory, roundId));
    }

    [Fact]
    public async Task SetExcludeFromStatsAsync_returns_false_for_a_missing_round()
    {
        var (service, _) = CreateService(nameof(SetExcludeFromStatsAsync_returns_false_for_a_missing_round));

        Assert.False(await service.SetExcludeFromStatsAsync(404, exclude: true, OwnerId));
    }

    [Fact]
    public async Task GetRoundsWithDetailsAsync_omits_excluded_rounds_when_no_filter_is_supplied()
    {
        // Regression guard: the exclusion used to sit inside the "if (filter is not null)" branch,
        // so an unfiltered call leaked excluded rounds into stats and /api/rounds/details.
        var (service, factory) = CreateService(nameof(GetRoundsWithDetailsAsync_omits_excluded_rounds_when_no_filter_is_supplied));
        var ids = await SeedRoundsAsync(factory, false, true);

        var rounds = await service.GetRoundsWithDetailsAsync(OwnerId, filter: null);

        Assert.Equal(new[] { ids[0] }, rounds.Select(r => r.RoundId));
    }

    [Fact]
    public async Task GetRoundsWithDetailsAsync_omits_excluded_rounds_when_a_filter_is_supplied()
    {
        var (service, factory) = CreateService(nameof(GetRoundsWithDetailsAsync_omits_excluded_rounds_when_a_filter_is_supplied));
        var ids = await SeedRoundsAsync(factory, false, true);

        var rounds = await service.GetRoundsWithDetailsAsync(OwnerId, new StatsFilter());

        Assert.Equal(new[] { ids[0] }, rounds.Select(r => r.RoundId));
    }

    [Fact]
    public async Task GetRoundsByUserIdAsync_keeps_excluded_rounds_in_the_display_list()
    {
        // The list is a round log, not a stats aggregate — hiding excluded rounds here would
        // leave the user no way to find one and un-exclude it.
        var (service, factory) = CreateService(nameof(GetRoundsByUserIdAsync_keeps_excluded_rounds_in_the_display_list));
        var ids = await SeedRoundsAsync(factory, false, true);

        var filtered = await service.GetRoundsByUserIdAsync(OwnerId, new StatsFilter());
        var unfiltered = await service.GetRoundsByUserIdAsync(OwnerId);

        Assert.Equal(ids.Count, filtered.Count);
        Assert.Equal(ids.Count, unfiltered.Count);
        Assert.True(filtered.Single(r => r.RoundId == ids[1]).ExcludeFromStats);
    }

    [Fact]
    public async Task GetPlayedCoursesByUserId_drops_courses_whose_only_rounds_are_excluded()
    {
        var (service, factory) = CreateService(nameof(GetPlayedCoursesByUserId_drops_courses_whose_only_rounds_are_excluded));
        await SeedRoundsAsync(factory, true);

        // statRounds scopes the result to rounds that count toward stats.
        var statCourses = await service.GetPlayedCoursesByUserId(OwnerId, statRounds: false);
        var allCourses = await service.GetPlayedCoursesByUserId(OwnerId);

        Assert.Empty(statCourses);
        Assert.Single(allCourses);
    }
}
