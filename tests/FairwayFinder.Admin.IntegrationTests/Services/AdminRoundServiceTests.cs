using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Admin;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Admin.IntegrationTests.Services;

/// <summary>
/// Cross-user round repair. The rule under test (CLAUDE.md, "Acting on another user's data"): the
/// acting admin and the round's owner stay separate, and the owner is re-resolved server-side.
/// </summary>
public class AdminRoundServiceTests(AdminFactory factory) : AdminTestBase(factory)
{
    [Fact]
    public async Task Admin_edit_keeps_the_round_with_its_owner_even_if_the_request_names_someone_else()
    {
        var admin = await Data.CreateAdminAsync();
        var golfer = await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await Data.CreateCompletedRoundAsync(golfer.Id, course);
        var service = Service<AdminRoundService>();
        var round = (await service.GetRoundDetailAsync(roundId))!;

        var updated = await service.UpdateRoundAsAdminAsync(new UpdateRoundRequest
        {
            RoundId = roundId,
            UserId = admin.Id, // a careless caller passing the acting admin — must be ignored
            TeeboxId = course.TeeboxId,
            DatePlayed = round.DatePlayed,
            Holes = round.Holes.Select(h => new HoleScoreEntry
            {
                ScoreId = h.ScoreId, HoleId = h.HoleId, HoleNumber = h.HoleNumber, Par = h.Par,
                Score = (short)(h.Par + 2),
            }).ToList(),
        }, admin.Id);

        Assert.True(updated);

        await using var db = Db();
        var stored = await db.Rounds.SingleAsync(r => r.RoundId == roundId);
        Assert.Equal(golfer.Id, stored.UserId);
        Assert.Equal(course.Par + 36, stored.Score);
        Assert.Equal(18, await db.Scores.CountAsync(s => s.RoundId == roundId && !s.IsDeleted));
    }

    [Fact]
    public async Task Admin_edit_of_a_missing_round_reports_false()
    {
        var admin = await Data.CreateAdminAsync();

        var updated = await Service<AdminRoundService>().UpdateRoundAsAdminAsync(
            new UpdateRoundRequest { RoundId = 987654, TeeboxId = 1, DatePlayed = new DateOnly(2026, 1, 1) }, admin.Id);

        Assert.False(updated);
    }

    [Fact]
    public async Task All_rounds_lists_every_golfers_rounds_including_open_ones()
    {
        var alice = await Data.CreateUserAsync();
        var bob = await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync();
        await Data.CreateCompletedRoundAsync(alice.Id, course);
        await Data.CreateCompletedRoundAsync(bob.Id, course);

        var rounds = await Service<AdminRoundService>().GetAllRoundsAsync();

        Assert.Equal(2, rounds.Count);
        Assert.Contains(rounds, r => r.UserId == alice.Id);
        Assert.Contains(rounds, r => r.UserId == bob.Id);
    }

    [Fact]
    public async Task Exclude_and_delete_work_across_owners_and_stamp_the_admin()
    {
        var admin = await Data.CreateAdminAsync();
        var golfer = await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await Data.CreateCompletedRoundAsync(golfer.Id, course);
        var service = Service<AdminRoundService>();

        Assert.True(await service.SetExcludeFromStatsAsync(roundId, true, admin.Id));
        await using (var db = Db())
        {
            var round = await db.Rounds.SingleAsync(r => r.RoundId == roundId);
            Assert.True(round.ExcludeFromStats);
            Assert.Equal(admin.Id, round.UpdatedBy);
            Assert.Equal(golfer.Id, round.UserId);
        }

        Assert.True(await service.DeleteRoundAsync(roundId, admin.Id));
        Assert.Null(await service.GetRoundDetailAsync(roundId));
        Assert.Empty(await service.GetRoundsForUserAsync(golfer.Id));
    }
}
