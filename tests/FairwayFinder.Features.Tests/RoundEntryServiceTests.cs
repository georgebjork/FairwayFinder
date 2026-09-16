using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// Covers hole-by-hole round entry: opening a round, writing holes as they are played, clearing
/// one, and posting. The through-line is that a round is not history until it is posted — so it
/// stays out of lists and stats until then, and friends hear about it exactly once.
/// </summary>
public class RoundEntryServiceTests
{
    private const string OwnerId = "owner-user";
    private const string OtherUserId = "other-user";
    private const string FriendId = "friend-user";

    /// <summary>Par for holes 1-18 on the seeded teebox: a standard 36/36, par 72.</summary>
    private static readonly int[] Pars = [4, 5, 3, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 4, 5, 4];

    private sealed record Harness(
        RoundEntryService Entry,
        RoundService Rounds,
        InMemoryDbContextFactory Factory,
        CountingPushNotificationService Push,
        long CourseId,
        long TeeboxId);

    private static async Task<Harness> CreateAsync(string dbName, bool nineHoleTeebox = false)
    {
        var factory = new InMemoryDbContextFactory(dbName);
        var push = new CountingPushNotificationService();
        var friends = new StubFriendService(FriendId);

        var rounds = new RoundService(factory, friends, push, NullLogger<RoundService>.Instance);
        var entry = new RoundEntryService(factory, rounds, friends, push, NullLogger<RoundEntryService>.Instance);

        await using var db = factory.CreateDbContext();

        var course = new Course { CourseName = "Test Course", CreatedBy = OwnerId, UpdatedBy = OwnerId };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var holeCount = nineHoleTeebox ? 9 : 18;
        var teebox = new Teebox
        {
            CourseId = course.CourseId,
            TeeboxName = "Blue",
            Par = Pars.Take(holeCount).Sum(),
            Rating = 71.5m,
            Slope = 130,
            IsNineHole = nineHoleTeebox,
            CreatedBy = OwnerId,
            UpdatedBy = OwnerId
        };
        db.Teeboxes.Add(teebox);
        await db.SaveChangesAsync();

        db.Holes.AddRange(Enumerable.Range(1, holeCount).Select(n => new Hole
        {
            TeeboxId = teebox.TeeboxId,
            CourseId = course.CourseId,
            HoleNumber = n,
            Par = Pars[n - 1],
            Yardage = 400,
            Handicap = n,
            CreatedBy = OwnerId,
            UpdatedBy = OwnerId
        }));

        db.Users.Add(new FairwayFinder.Identity.ApplicationUser
        {
            Id = OwnerId,
            UserName = "owner@test.com",
            Email = "owner@test.com",
            FirstName = "Owner",
            LastName = "User"
        });

        await db.SaveChangesAsync();

        return new Harness(entry, rounds, factory, push, course.CourseId, teebox.TeeboxId);
    }

    private static StartRoundRequest StartRequest(Harness h, bool fullRound = true, bool frontNine = false,
        bool backNine = false, bool holeStats = false, bool shotTracking = false) => new()
    {
        CourseId = h.CourseId,
        TeeboxId = h.TeeboxId,
        DatePlayed = new DateOnly(2026, 7, 1),
        FullRound = fullRound,
        FrontNine = frontNine,
        BackNine = backNine,
        UsingHoleStats = holeStats,
        UsingShotTracking = shotTracking
    };

    /// <summary>Plays the given holes at par, one upsert each — the normal path through a round.</summary>
    private static async Task PlayHolesAsync(Harness h, long roundId, IEnumerable<int> holeNumbers)
    {
        foreach (var n in holeNumbers)
        {
            var result = await h.Entry.UpsertHoleAsync(roundId, n, new UpsertHoleRequest { Score = (short)Pars[n - 1] }, OwnerId);
            Assert.True(result.IsOk);
        }
    }

    // ── Starting a round ──

    [Fact]
    public async Task StartRoundAsync_opens_an_empty_round_that_is_not_yet_complete()
    {
        var h = await CreateAsync(nameof(StartRoundAsync_opens_an_empty_round_that_is_not_yet_complete));

        var result = await h.Entry.StartRoundAsync(StartRequest(h), OwnerId);

        Assert.True(result.IsOk);
        Assert.Equal(18, result.Value!.Holes.Count);
        Assert.Equal(h.TeeboxId, result.Value.TeeboxId);

        await using var db = h.Factory.CreateDbContext();
        var round = await db.Rounds.SingleAsync(r => r.RoundId == result.Value.RoundId);

        Assert.False(round.IsComplete);
        Assert.Equal(0, round.Score);
        Assert.Equal(0, round.ScoreOut);
        Assert.Equal(0, round.ScoreIn);
        Assert.Empty(await db.Scores.Where(s => s.RoundId == round.RoundId).ToListAsync());
        Assert.Empty(await db.RoundStats.Where(rs => rs.RoundId == round.RoundId).ToListAsync());
    }

    [Fact]
    public async Task StartRoundAsync_does_not_notify_friends()
    {
        var h = await CreateAsync(nameof(StartRoundAsync_does_not_notify_friends));

        await h.Entry.StartRoundAsync(StartRequest(h), OwnerId);

        Assert.Empty(h.Push.Sent);
    }

    [Fact]
    public async Task StartRoundAsync_refuses_a_second_round_and_names_the_open_one()
    {
        var h = await CreateAsync(nameof(StartRoundAsync_refuses_a_second_round_and_names_the_open_one));
        var first = await h.Entry.StartRoundAsync(StartRequest(h), OwnerId);

        var second = await h.Entry.StartRoundAsync(StartRequest(h), OwnerId);

        Assert.Equal(RoundEntryStatus.ActiveRoundExists, second.Status);
        Assert.Equal(first.Value!.RoundId, second.ConflictingRoundId);
    }

    [Fact]
    public async Task StartRoundAsync_allows_a_new_round_once_the_open_one_is_discarded()
    {
        var h = await CreateAsync(nameof(StartRoundAsync_allows_a_new_round_once_the_open_one_is_discarded));
        var first = await h.Entry.StartRoundAsync(StartRequest(h), OwnerId);

        await h.Rounds.DeleteRoundAsync(first.Value!.RoundId, OwnerId);
        var second = await h.Entry.StartRoundAsync(StartRequest(h), OwnerId);

        Assert.True(second.IsOk);
        Assert.NotEqual(first.Value.RoundId, second.Value!.RoundId);
    }

    [Fact]
    public async Task StartRoundAsync_refuses_an_archived_teebox()
    {
        var h = await CreateAsync(nameof(StartRoundAsync_refuses_an_archived_teebox));

        await using (var db = h.Factory.CreateDbContext())
        {
            var teebox = await db.Teeboxes.SingleAsync(t => t.TeeboxId == h.TeeboxId);
            teebox.ArchivedOn = DateOnly.FromDateTime(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        var result = await h.Entry.StartRoundAsync(StartRequest(h), OwnerId);

        Assert.Equal(RoundEntryStatus.TeeboxArchived, result.Status);
    }

    // ── Writing holes ──

    [Fact]
    public async Task UpsertHoleAsync_writes_a_hole_and_reports_the_running_total()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_writes_a_hole_and_reports_the_running_total));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        var first = await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = 5 }, OwnerId);
        var second = await h.Entry.UpsertHoleAsync(roundId, 2, new UpsertHoleRequest { Score = 6 }, OwnerId);

        Assert.True(first.IsOk);
        Assert.Equal(5, first.Value!.Total);
        Assert.Equal(1, first.Value.HolesEntered);
        Assert.Equal(1, first.Value.ToPar); // hole 1 is a par 4

        Assert.Equal(11, second.Value!.Total);
        Assert.Equal(11, second.Value.ScoreOut);
        Assert.Equal(0, second.Value.ScoreIn);
        Assert.Equal(2, second.Value.HolesEntered);
        Assert.Equal(2, second.Value.ToPar); // 11 against a par-9 pair of holes
    }

    [Fact]
    public async Task UpsertHoleAsync_is_idempotent_and_overwrites_rather_than_duplicating()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_is_idempotent_and_overwrites_rather_than_duplicating));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = 7 }, OwnerId);
        var corrected = await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = 4 }, OwnerId);

        Assert.Equal(4, corrected.Value!.Total);
        Assert.Equal(1, corrected.Value.HolesEntered);

        await using var db = h.Factory.CreateDbContext();
        var scores = await db.Scores.Where(s => s.RoundId == roundId && !s.IsDeleted).ToListAsync();

        Assert.Single(scores);
        Assert.Equal((short)4, scores[0].HoleScore);
        Assert.Equal(4, (await db.Rounds.SingleAsync(r => r.RoundId == roundId)).Score);
    }

    [Fact]
    public async Task UpsertHoleAsync_splits_the_total_across_the_turn()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_splits_the_total_across_the_turn));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        await h.Entry.UpsertHoleAsync(roundId, 9, new UpsertHoleRequest { Score = 5 }, OwnerId);
        var result = await h.Entry.UpsertHoleAsync(roundId, 10, new UpsertHoleRequest { Score = 6 }, OwnerId);

        Assert.Equal(5, result.Value!.ScoreOut);
        Assert.Equal(6, result.Value.ScoreIn);
        Assert.Equal(11, result.Value.Total);
    }

    [Fact]
    public async Task UpsertHoleAsync_takes_the_hole_and_its_par_from_the_rounds_own_teebox()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_takes_the_hole_and_its_par_from_the_rounds_own_teebox));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        // Hole 2 is the par 5 on this teebox. The client sends only the number.
        var result = await h.Entry.UpsertHoleAsync(roundId, 2, new UpsertHoleRequest { Score = 5 }, OwnerId);

        await using var db = h.Factory.CreateDbContext();
        var expectedHole = await db.Holes.SingleAsync(x => x.TeeboxId == h.TeeboxId && x.HoleNumber == 2);

        Assert.Equal(expectedHole.HoleId, result.Value!.HoleId);
        Assert.Equal(5, result.Value.Par);
        Assert.Equal(0, result.Value.ToPar); // par 5 played in 5
    }

    [Fact]
    public async Task UpsertHoleAsync_rejects_a_hole_number_the_teebox_does_not_have()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_rejects_a_hole_number_the_teebox_does_not_have), nineHoleTeebox: true);
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h, fullRound: false, frontNine: true), OwnerId)).Value!.RoundId;

        var result = await h.Entry.UpsertHoleAsync(roundId, 14, new UpsertHoleRequest { Score = 4 }, OwnerId);

        Assert.Equal(RoundEntryStatus.HoleNotOnTeebox, result.Status);
    }

    [Fact]
    public async Task UpsertHoleAsync_rejects_a_user_who_does_not_own_the_round()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_rejects_a_user_who_does_not_own_the_round));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        var result = await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = 4 }, OtherUserId);

        Assert.Equal(RoundEntryStatus.NotOwner, result.Status);
    }

    [Fact]
    public async Task UpsertHoleAsync_refuses_a_round_that_has_already_been_posted()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_refuses_a_round_that_has_already_been_posted));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, Enumerable.Range(1, 18));
        await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        var result = await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = 9 }, OwnerId);

        Assert.Equal(RoundEntryStatus.RoundAlreadyComplete, result.Status);
    }

    [Fact]
    public async Task UpsertHoleAsync_stores_hole_stats_when_the_round_tracks_them()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_stores_hole_stats_when_the_round_tracks_them));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h, holeStats: true), OwnerId)).Value!.RoundId;

        await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest
        {
            Score = 5,
            HitFairway = true,
            HitGreen = false,
            NumberOfPutts = 2,
            ApproachYardage = 150
        }, OwnerId);

        await using var db = h.Factory.CreateDbContext();
        var stat = await db.HoleStats.SingleAsync(hs => hs.RoundId == roundId && !hs.IsDeleted);

        Assert.True(stat.HitFairway);
        Assert.False(stat.HitGreen);
        Assert.Equal((short)2, stat.NumberOfPutts);
        Assert.Equal(150, stat.ApproachYardage);
    }

    [Fact]
    public async Task UpsertHoleAsync_derives_hole_stats_from_shots_and_renumbers_them()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_derives_hole_stats_from_shots_and_renumbers_them));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h, shotTracking: true), OwnerId)).Value!.RoundId;

        // A par 4 played in 4: drive to the fairway, approach to the green, two putts.
        // ShotNumber is deliberately scrambled — the server owns the ordering.
        await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest
        {
            Score = 4,
            Shots =
            [
                new ShotData { ShotNumber = 9, StartDistance = 400, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Tee, EndDistance = 150, EndDistanceUnit = DistanceUnit.Yards, EndLie = LieType.Fairway },
                new ShotData { ShotNumber = 7, StartDistance = 150, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Fairway, EndDistance = 20, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green },
                new ShotData { ShotNumber = 3, StartDistance = 20, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green, EndDistance = 3, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green },
                new ShotData { ShotNumber = 1, StartDistance = 3, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green }
            ]
        }, OwnerId);

        await using var db = h.Factory.CreateDbContext();
        var score = await db.Scores.SingleAsync(s => s.RoundId == roundId && !s.IsDeleted);
        var shots = await db.Shots.Where(s => s.ScoreId == score.ScoreId && !s.IsDeleted)
            .OrderBy(s => s.ShotNumber).ToListAsync();
        var stat = await db.HoleStats.SingleAsync(hs => hs.RoundId == roundId && !hs.IsDeleted);

        Assert.Equal([1, 2, 3, 4], shots.Select(s => s.ShotNumber));
        Assert.True(stat.HitFairway);
        Assert.True(stat.HitGreen);
        Assert.Equal((short)2, stat.NumberOfPutts);
    }

    [Fact]
    public async Task UpsertHoleAsync_replaces_a_holes_shots_rather_than_appending_to_them()
    {
        var h = await CreateAsync(nameof(UpsertHoleAsync_replaces_a_holes_shots_rather_than_appending_to_them));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h, shotTracking: true), OwnerId)).Value!.RoundId;

        var threeShots = new List<ShotData>
        {
            new() { StartDistance = 400, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Tee, EndDistance = 150, EndDistanceUnit = DistanceUnit.Yards, EndLie = LieType.Fairway },
            new() { StartDistance = 150, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Fairway, EndDistance = 4, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green },
            new() { StartDistance = 4, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green }
        };

        await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = 4, Shots = [.. threeShots, new ShotData { StartDistance = 2, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green }] }, OwnerId);
        await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = 3, Shots = threeShots }, OwnerId);

        await using var db = h.Factory.CreateDbContext();
        var score = await db.Scores.SingleAsync(s => s.RoundId == roundId && !s.IsDeleted);
        var live = await db.Shots.Where(s => s.ScoreId == score.ScoreId && !s.IsDeleted).ToListAsync();

        Assert.Equal(3, live.Count);
        Assert.Equal([1, 2, 3], live.OrderBy(s => s.ShotNumber).Select(s => s.ShotNumber));
    }

    // ── Clearing a hole ──

    [Fact]
    public async Task ClearHoleAsync_removes_the_hole_and_its_detail_then_recomputes()
    {
        var h = await CreateAsync(nameof(ClearHoleAsync_removes_the_hole_and_its_detail_then_recomputes));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h, holeStats: true), OwnerId)).Value!.RoundId;

        await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = 5, NumberOfPutts = 2 }, OwnerId);
        await h.Entry.UpsertHoleAsync(roundId, 2, new UpsertHoleRequest { Score = 6, NumberOfPutts = 2 }, OwnerId);

        var result = await h.Entry.ClearHoleAsync(roundId, 1, OwnerId);

        Assert.True(result.IsOk);
        Assert.Null(result.Value!.Score);
        Assert.Equal(6, result.Value.Total);
        Assert.Equal(1, result.Value.HolesEntered);

        await using var db = h.Factory.CreateDbContext();
        Assert.Single(await db.Scores.Where(s => s.RoundId == roundId && !s.IsDeleted).ToListAsync());
        Assert.Single(await db.HoleStats.Where(hs => hs.RoundId == roundId && !hs.IsDeleted).ToListAsync());
        Assert.Equal(6, (await db.Rounds.SingleAsync(r => r.RoundId == roundId)).Score);
    }

    [Fact]
    public async Task ClearHoleAsync_succeeds_on_a_hole_that_was_never_entered()
    {
        var h = await CreateAsync(nameof(ClearHoleAsync_succeeds_on_a_hole_that_was_never_entered));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        var result = await h.Entry.ClearHoleAsync(roundId, 7, OwnerId);

        Assert.True(result.IsOk);
        Assert.Equal(0, result.Value!.HolesEntered);
    }

    [Fact]
    public async Task A_cleared_hole_can_be_entered_again()
    {
        var h = await CreateAsync(nameof(A_cleared_hole_can_be_entered_again));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        await h.Entry.UpsertHoleAsync(roundId, 3, new UpsertHoleRequest { Score = 4 }, OwnerId);
        await h.Entry.ClearHoleAsync(roundId, 3, OwnerId);
        var again = await h.Entry.UpsertHoleAsync(roundId, 3, new UpsertHoleRequest { Score = 3 }, OwnerId);

        Assert.True(again.IsOk);
        Assert.Equal(3, again.Value!.Total);
        Assert.Equal(1, again.Value.HolesEntered);
    }

    // ── Resuming ──

    [Fact]
    public async Task GetActiveRoundAsync_returns_the_round_in_progress_with_the_holes_so_far()
    {
        var h = await CreateAsync(nameof(GetActiveRoundAsync_returns_the_round_in_progress_with_the_holes_so_far));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, [1, 2, 3]);

        var active = await h.Entry.GetActiveRoundAsync(OwnerId, BaselineLevel.Scratch);

        Assert.NotNull(active);
        Assert.Equal(roundId, active!.RoundId);
        Assert.False(active.IsComplete);
        Assert.Equal(3, active.Holes.Count);
        Assert.Equal(Pars[0] + Pars[1] + Pars[2], active.Score);
        // To-par measures against the holes played, not the whole teebox.
        Assert.Equal(0, active.ScoreToPar);
    }

    [Fact]
    public async Task GetActiveRoundAsync_returns_nothing_when_no_round_is_open()
    {
        var h = await CreateAsync(nameof(GetActiveRoundAsync_returns_nothing_when_no_round_is_open));

        Assert.Null(await h.Entry.GetActiveRoundAsync(OwnerId, BaselineLevel.Scratch));
    }

    [Fact]
    public async Task GetActiveRoundAsync_ignores_a_round_that_has_been_posted()
    {
        var h = await CreateAsync(nameof(GetActiveRoundAsync_ignores_a_round_that_has_been_posted));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, Enumerable.Range(1, 18));
        await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        Assert.Null(await h.Entry.GetActiveRoundAsync(OwnerId, BaselineLevel.Scratch));
    }

    // ── Posting ──

    [Fact]
    public async Task CompleteRoundAsync_refuses_a_round_with_holes_missing_and_names_them()
    {
        var h = await CreateAsync(nameof(CompleteRoundAsync_refuses_a_round_with_holes_missing_and_names_them));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, Enumerable.Range(1, 18).Where(n => n != 7 && n != 12));

        var result = await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        Assert.Equal(RoundEntryStatus.RoundIncomplete, result.Status);
        Assert.Equal([7, 12], result.MissingHoles);
        Assert.Empty(h.Push.Sent);

        await using var db = h.Factory.CreateDbContext();
        Assert.False((await db.Rounds.SingleAsync(r => r.RoundId == roundId)).IsComplete);
    }

    [Fact]
    public async Task CompleteRoundAsync_refuses_a_round_with_no_holes_at_all()
    {
        var h = await CreateAsync(nameof(CompleteRoundAsync_refuses_a_round_with_no_holes_at_all));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        var result = await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        Assert.Equal(RoundEntryStatus.RoundIncomplete, result.Status);
        Assert.Equal(18, result.MissingHoles.Count);
    }

    [Fact]
    public async Task CompleteRoundAsync_posts_a_full_eighteen_with_its_scoring_distribution()
    {
        var h = await CreateAsync(nameof(CompleteRoundAsync_posts_a_full_eighteen_with_its_scoring_distribution));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;

        // Par on every hole except a birdie on 1 and a double on 2.
        await PlayHolesAsync(h, roundId, Enumerable.Range(3, 16));
        await h.Entry.UpsertHoleAsync(roundId, 1, new UpsertHoleRequest { Score = (short)(Pars[0] - 1) }, OwnerId);
        await h.Entry.UpsertHoleAsync(roundId, 2, new UpsertHoleRequest { Score = (short)(Pars[1] + 2) }, OwnerId);

        var result = await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        Assert.True(result.IsOk);
        Assert.True(result.Value!.IsComplete);

        await using var db = h.Factory.CreateDbContext();
        var round = await db.Rounds.SingleAsync(r => r.RoundId == roundId);
        var stat = await db.RoundStats.SingleAsync(rs => rs.RoundId == roundId && !rs.IsDeleted);

        Assert.True(round.IsComplete);
        Assert.True(round.FullRound);
        Assert.True(round.FrontNine);
        Assert.True(round.BackNine);
        Assert.Equal(Pars.Sum() + 1, round.Score); // -1 birdie, +2 double
        Assert.Equal(Pars.Take(9).Sum() + 1, round.ScoreOut);
        Assert.Equal(Pars.Skip(9).Sum(), round.ScoreIn);

        Assert.Equal(1, stat.Birdies);
        Assert.Equal(1, stat.DoubleBogies);
        Assert.Equal(16, stat.Pars);
        Assert.Equal(0, stat.Bogies);
    }

    [Fact]
    public async Task CompleteRoundAsync_posts_a_back_nine_and_records_which_nine_it_was()
    {
        var h = await CreateAsync(nameof(CompleteRoundAsync_posts_a_back_nine_and_records_which_nine_it_was));
        // The golfer said "front nine" at the first tee but played the back. The holes win.
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h, fullRound: false, frontNine: true), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, Enumerable.Range(10, 9));

        var result = await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        Assert.True(result.IsOk);

        await using var db = h.Factory.CreateDbContext();
        var round = await db.Rounds.SingleAsync(r => r.RoundId == roundId);

        Assert.True(round.IsComplete);
        Assert.False(round.FullRound);
        Assert.False(round.FrontNine);
        Assert.True(round.BackNine);
        Assert.Equal(0, round.ScoreOut);
        Assert.Equal(Pars.Skip(9).Sum(), round.ScoreIn);
    }

    [Fact]
    public async Task CompleteRoundAsync_posts_a_nine_hole_teebox_round()
    {
        var h = await CreateAsync(nameof(CompleteRoundAsync_posts_a_nine_hole_teebox_round), nineHoleTeebox: true);
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h, fullRound: true), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, Enumerable.Range(1, 9));

        var result = await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        Assert.True(result.IsOk);

        await using var db = h.Factory.CreateDbContext();
        var round = await db.Rounds.SingleAsync(r => r.RoundId == roundId);

        // Covering every hole the teebox has is a full round, even though there are only nine.
        Assert.True(round.FullRound);
        Assert.True(round.FrontNine);
        Assert.False(round.BackNine);
    }

    [Fact]
    public async Task CompleteRoundAsync_notifies_friends_once_and_not_again_on_a_retry()
    {
        var h = await CreateAsync(nameof(CompleteRoundAsync_notifies_friends_once_and_not_again_on_a_retry));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, Enumerable.Range(1, 18));

        var first = await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);
        var retry = await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        Assert.True(first.IsOk);
        Assert.True(retry.IsOk);
        Assert.Equal(first.Value!.Score, retry.Value!.Score);

        var sent = Assert.Single(h.Push.Sent);
        Assert.Equal(FriendId, sent.UserId);
        Assert.Contains("posted a round", sent.Title);
    }

    [Fact]
    public async Task CompleteRoundAsync_rejects_a_user_who_does_not_own_the_round()
    {
        var h = await CreateAsync(nameof(CompleteRoundAsync_rejects_a_user_who_does_not_own_the_round));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, Enumerable.Range(1, 18));

        var result = await h.Entry.CompleteRoundAsync(roundId, OtherUserId, BaselineLevel.Scratch);

        Assert.Equal(RoundEntryStatus.NotOwner, result.Status);
    }

    // ── Visibility ──

    [Fact]
    public async Task A_round_in_progress_stays_out_of_lists_and_stats_until_it_is_posted()
    {
        var h = await CreateAsync(nameof(A_round_in_progress_stays_out_of_lists_and_stats_until_it_is_posted));
        var roundId = (await h.Entry.StartRoundAsync(StartRequest(h), OwnerId)).Value!.RoundId;
        await PlayHolesAsync(h, roundId, Enumerable.Range(1, 18));

        Assert.Empty(await h.Rounds.GetRoundsByUserIdAsync(OwnerId));
        Assert.Empty(await h.Rounds.GetRoundsWithDetailsAsync(OwnerId));
        Assert.Empty(await h.Rounds.GetPlayedCoursesByUserId(OwnerId));
        // The owner can still reach it directly — that is what resume and the admin console use.
        Assert.NotNull(await h.Rounds.GetRoundByIdAsync(roundId));

        await h.Entry.CompleteRoundAsync(roundId, OwnerId, BaselineLevel.Scratch);

        Assert.Single(await h.Rounds.GetRoundsByUserIdAsync(OwnerId));
        Assert.Single(await h.Rounds.GetRoundsWithDetailsAsync(OwnerId));
        Assert.Single(await h.Rounds.GetPlayedCoursesByUserId(OwnerId));
    }
}
