using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Services.Admin;
using FairwayFinder.Features.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// The admin repair surface. The rule under test throughout is the one
/// <c>AdminRoundService.UpdateRoundAsAdminAsync</c> establishes: an admin never gets a looser
/// guard, only the correct identity resolved server-side. Every participant method here takes a
/// participant id and nothing else, so there is no input that could move a participant between
/// games or hand their line to another golfer.
/// </summary>
public class AdminGameServiceTests
{
    private const string HostId = "host-user";
    private const string FriendId = "friend-user";
    private const string StrangerId = "stranger-user";
    private const string AdminId = "admin-user";

    private static readonly int[] Pars = [4, 5, 3, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 4, 5, 4];

    private sealed record Harness(
        AdminGameService Admin,
        GameService Games,
        RoundEntryService Entry,
        InMemoryDbContextFactory Factory,
        long CourseId,
        long TeeboxId,
        long SecondTeeboxId,
        long ArchivedTeeboxId);

    private static async Task<Harness> CreateAsync(string dbName)
    {
        var factory = new InMemoryDbContextFactory(dbName);
        var push = new CountingPushNotificationService();
        var friends = new StubFriendService(FriendId);

        var rounds = new RoundService(factory, friends, push, NullLogger<RoundService>.Instance);
        var entry = new RoundEntryService(factory, rounds, friends, push, NullLogger<RoundEntryService>.Instance);

        var resolver = new GameScoringEngineResolver([new MatchPlayScoringEngine(), new SkinsScoringEngine()]);
        var reader = new GameScoreReader(factory);
        var games = new GameService(factory, resolver, reader, friends, push, NullLogger<GameService>.Instance);
        var admin = new AdminGameService(games, reader, resolver, factory, NullLogger<AdminGameService>.Instance);

        await using var db = factory.CreateDbContext();

        var course = new Course { CourseName = "Test Course", CreatedBy = HostId, UpdatedBy = HostId };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var blue = NewTeebox(course.CourseId, "Blue");
        var white = NewTeebox(course.CourseId, "White");
        var archived = NewTeebox(course.CourseId, "Old Blue");
        archived.ArchivedOn = new DateOnly(2026, 1, 1);

        db.Teeboxes.AddRange(blue, white, archived);
        await db.SaveChangesAsync();

        foreach (var teebox in new[] { blue, white, archived })
        {
            db.Holes.AddRange(Enumerable.Range(1, 18).Select(n => new Hole
            {
                TeeboxId = teebox.TeeboxId,
                CourseId = course.CourseId,
                HoleNumber = n,
                Par = Pars[n - 1],
                Yardage = 400,
                Handicap = n,
                CreatedBy = HostId,
                UpdatedBy = HostId
            }));
        }

        db.Users.AddRange(
            NewUser(HostId, "Dale", "Host"),
            NewUser(FriendId, "Sam", "Friend"),
            NewUser(StrangerId, "Kit", "Stranger"));

        await db.SaveChangesAsync();

        return new Harness(admin, games, entry, factory, course.CourseId,
            blue.TeeboxId, white.TeeboxId, archived.TeeboxId);
    }

    private static Teebox NewTeebox(long courseId, string name) => new()
    {
        CourseId = courseId,
        TeeboxName = name,
        Par = Pars.Sum(),
        Rating = 71.5m,
        Slope = 130,
        CreatedBy = HostId,
        UpdatedBy = HostId
    };

    private static FairwayFinder.Identity.ApplicationUser NewUser(string id, string first, string last) => new()
    {
        Id = id,
        UserName = $"{id}@test.com",
        Email = $"{id}@test.com",
        FirstName = first,
        LastName = last
    };

    /// <summary>A started match between the host and their friend, both on the Blue tees.</summary>
    private static async Task<(long GameId, long HostPid, long FriendPid)> StartedGameAsync(
        Harness h, int hostHandicap = 0, int friendHandicap = 0, bool useNet = false)
    {
        var created = await h.Games.CreateGameAsync(new CreateGameRequest
        {
            GameType = GameType.MatchPlay,
            CourseId = h.CourseId,
            TeeboxId = h.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = true,
            CourseHandicap = hostHandicap,
            UseNet = useNet
        }, HostId);

        var gameId = created.Value!.GameId;

        var joined = await h.Games.JoinGameAsync(new JoinGameRequest
        {
            JoinCode = created.Value.JoinCode,
            TeeboxId = h.TeeboxId,
            CourseHandicap = friendHandicap
        }, FriendId);

        await h.Games.StartGameAsync(gameId, HostId);

        return (gameId,
            joined.Value!.Participants.Single(p => p.UserId == HostId).ParticipantId,
            joined.Value.Participants.Single(p => p.UserId == FriendId).ParticipantId);
    }

    private static async Task<long> StartRoundAsync(Harness h, string userId, long teeboxId, long courseId)
    {
        var started = await h.Entry.StartRoundAsync(new StartRoundRequest
        {
            CourseId = courseId,
            TeeboxId = teeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = true
        }, userId);

        return started.Value!.RoundId;
    }

    // ── Listing and reading ──

    [Fact]
    public async Task GetAllGamesAsync_lists_games_across_users_with_the_host_named()
    {
        var h = await CreateAsync(nameof(GetAllGamesAsync_lists_games_across_users_with_the_host_named));
        await StartedGameAsync(h);

        var games = await h.Admin.GetAllGamesAsync();

        var game = Assert.Single(games);
        Assert.Equal("Dale Host", game.HostName);
        Assert.Equal("host-user@test.com", game.HostEmail);
        Assert.Equal(2, game.ParticipantCount);
        Assert.Equal("18", game.Shape);
        Assert.Equal(GameState.Active, game.State);
    }

    [Fact]
    public async Task GetAllGamesAsync_labels_a_nine_hole_games_shape()
    {
        var h = await CreateAsync(nameof(GetAllGamesAsync_labels_a_nine_hole_games_shape));

        await h.Games.CreateGameAsync(new CreateGameRequest
        {
            GameType = GameType.Skins,
            CourseId = h.CourseId,
            TeeboxId = h.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = false,
            BackNine = true
        }, HostId);

        var game = Assert.Single(await h.Admin.GetAllGamesAsync());
        Assert.Equal("Back 9", game.Shape);
    }

    [Fact]
    public async Task GetGameForAdminAsync_reads_a_game_the_admin_is_not_in()
    {
        var h = await CreateAsync(nameof(GetGameForAdminAsync_reads_a_game_the_admin_is_not_in));
        var (gameId, _, _) = await StartedGameAsync(h);

        var detail = await h.Admin.GetGameForAdminAsync(gameId);

        Assert.NotNull(detail);
        Assert.Equal("Dale Host", detail.HostName);
        Assert.Equal(2, detail.State.Participants.Count);

        // The scorecard grid renders off these; without them the page has nothing to show.
        Assert.Equal(2, detail.Lines.Count);
        Assert.Equal(18, detail.Lines.Values.First().Count);
    }

    [Fact]
    public async Task GetGameForAdminAsync_returns_null_for_a_game_that_does_not_exist()
    {
        var h = await CreateAsync(nameof(GetGameForAdminAsync_returns_null_for_a_game_that_does_not_exist));

        Assert.Null(await h.Admin.GetGameForAdminAsync(9999));
    }

    // ── Repair options ──

    [Fact]
    public async Task GetRepairTeeboxOptionsAsync_offers_archived_tees_but_flags_them()
    {
        var h = await CreateAsync(nameof(GetRepairTeeboxOptionsAsync_offers_archived_tees_but_flags_them));
        var (gameId, _, _) = await StartedGameAsync(h);

        var options = await h.Admin.GetRepairTeeboxOptionsAsync(gameId);

        // A game genuinely played off a since-replaced tee has to stay repairable, so the
        // create-time archived guard deliberately does not apply here.
        var archived = options.Single(o => o.TeeboxId == h.ArchivedTeeboxId);
        Assert.True(archived.IsArchived);
        Assert.True(archived.CoversGameHoles);
        Assert.Contains("archived", archived.Label);

        Assert.False(options.Single(o => o.TeeboxId == h.TeeboxId).IsArchived);
    }

    [Fact]
    public async Task GetRelinkCandidatesAsync_offers_only_that_players_rounds_on_this_course()
    {
        var h = await CreateAsync(nameof(GetRelinkCandidatesAsync_offers_only_that_players_rounds_on_this_course));
        var (_, hostPid, friendPid) = await StartedGameAsync(h);

        var hostRound = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        await StartRoundAsync(h, FriendId, h.TeeboxId, h.CourseId);

        var forHost = await h.Admin.GetRelinkCandidatesAsync(hostPid);

        var option = Assert.Single(forHost);
        Assert.Equal(hostRound, option.RoundId);
        Assert.Equal("Blue", option.TeeboxName);

        // The friend's own round is theirs alone — the host's list must not contain it.
        Assert.DoesNotContain(await h.Admin.GetRelinkCandidatesAsync(friendPid), r => r.RoundId == hostRound);
    }

    [Fact]
    public async Task GetRelinkCandidatesAsync_is_empty_for_a_guest()
    {
        var h = await CreateAsync(nameof(GetRelinkCandidatesAsync_is_empty_for_a_guest));

        var created = await h.Games.CreateGameAsync(new CreateGameRequest
        {
            GameType = GameType.Skins,
            CourseId = h.CourseId,
            TeeboxId = h.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = true
        }, HostId);

        var added = await h.Games.AddParticipantAsync(created.Value!.GameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        var guestPid = added.Value!.Participants.Single(p => p.IsGuest).ParticipantId;

        Assert.Empty(await h.Admin.GetRelinkCandidatesAsync(guestPid));
    }

    // ── Repair ──

    [Fact]
    public async Task RepairParticipantAsync_fixing_a_handicap_moves_the_live_scoreboard()
    {
        var h = await CreateAsync(nameof(RepairParticipantAsync_fixing_a_handicap_moves_the_live_scoreboard));

        // Net match, both off scratch. The host bogeys hole 1, the friend makes par.
        var (gameId, hostPid, friendPid) = await StartedGameAsync(h, useNet: true);

        var hostRound = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        var friendRound = await StartRoundAsync(h, FriendId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, hostRound, HostId);
        await h.Games.LinkRoundAsync(gameId, friendRound, FriendId);

        await h.Entry.UpsertHoleAsync(hostRound, 1, new UpsertHoleRequest { Score = (short)(Pars[0] + 1) }, HostId);
        await h.Entry.UpsertHoleAsync(friendRound, 1, new UpsertHoleRequest { Score = (short)Pars[0] }, FriendId);

        var before = (MatchPlayScoreboard)(await h.Admin.GetGameForAdminAsync(gameId))!.State.Scoreboard!;
        Assert.Equal(friendPid, before.LeaderParticipantId);

        // The host actually plays off 1 and gets a shot on the hardest hole. Nothing downstream
        // is stored, so the corrected board appears on the very next read.
        var repaired = await h.Admin.RepairParticipantAsync(
            hostPid, new RepairParticipantRequest { CourseHandicap = 1 }, AdminId);

        Assert.True(repaired);

        var after = (MatchPlayScoreboard)(await h.Admin.GetGameForAdminAsync(gameId))!.State.Scoreboard!;
        Assert.True(after.Holes[0].IsHalved);
        Assert.Null(after.LeaderParticipantId);
    }

    [Fact]
    public async Task RepairParticipantAsync_stamps_the_admin_not_the_player()
    {
        var h = await CreateAsync(nameof(RepairParticipantAsync_stamps_the_admin_not_the_player));
        var (_, hostPid, _) = await StartedGameAsync(h);

        await h.Admin.RepairParticipantAsync(hostPid, new RepairParticipantRequest { CourseHandicap = 7 }, AdminId);

        await using var db = h.Factory.CreateDbContext();
        var participant = await db.GameParticipants.SingleAsync(p => p.GameParticipantId == hostPid);

        Assert.Equal(7, participant.CourseHandicap);
        Assert.Equal(AdminId, participant.UpdatedBy);

        // The participant still belongs to the same golfer — repair never reassigns a line.
        Assert.Equal(HostId, participant.UserId);
    }

    [Fact]
    public async Task RepairParticipantAsync_moves_a_participant_to_another_teebox()
    {
        var h = await CreateAsync(nameof(RepairParticipantAsync_moves_a_participant_to_another_teebox));
        var (gameId, hostPid, _) = await StartedGameAsync(h);

        var repaired = await h.Admin.RepairParticipantAsync(
            hostPid, new RepairParticipantRequest { TeeboxId = h.SecondTeeboxId }, AdminId);

        Assert.True(repaired);

        var detail = await h.Admin.GetGameForAdminAsync(gameId);
        Assert.Equal("White", detail!.State.Participants.Single(p => p.ParticipantId == hostPid).TeeboxName);
    }

    [Fact]
    public async Task RepairParticipantAsync_refuses_to_change_tees_on_a_linked_round()
    {
        var h = await CreateAsync(nameof(RepairParticipantAsync_refuses_to_change_tees_on_a_linked_round));
        var (gameId, hostPid, _) = await StartedGameAsync(h);

        var round = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);

        // The round's scores are recorded against its own teebox's holes, so the tees cannot be
        // moved out from under them.
        var repaired = await h.Admin.RepairParticipantAsync(
            hostPid, new RepairParticipantRequest { TeeboxId = h.SecondTeeboxId }, AdminId);

        Assert.False(repaired);
    }

    [Fact]
    public async Task RepairParticipantAsync_refuses_a_teebox_from_another_course()
    {
        var h = await CreateAsync(nameof(RepairParticipantAsync_refuses_a_teebox_from_another_course));
        var (_, hostPid, _) = await StartedGameAsync(h);

        long strayTeeboxId;
        await using (var db = h.Factory.CreateDbContext())
        {
            var other = new Course { CourseName = "Elsewhere", CreatedBy = AdminId, UpdatedBy = AdminId };
            db.Courses.Add(other);
            await db.SaveChangesAsync();

            var teebox = NewTeebox(other.CourseId, "Stray");
            db.Teeboxes.Add(teebox);
            await db.SaveChangesAsync();
            strayTeeboxId = teebox.TeeboxId;
        }

        Assert.False(await h.Admin.RepairParticipantAsync(
            hostPid, new RepairParticipantRequest { TeeboxId = strayTeeboxId }, AdminId));
    }

    [Fact]
    public async Task RepairParticipantAsync_returns_false_for_a_participant_that_does_not_exist()
    {
        var h = await CreateAsync(nameof(RepairParticipantAsync_returns_false_for_a_participant_that_does_not_exist));

        Assert.False(await h.Admin.RepairParticipantAsync(
            9999, new RepairParticipantRequest { CourseHandicap = 3 }, AdminId));
    }

    // ── Relinking ──

    [Fact]
    public async Task RelinkParticipantRoundAsync_forces_the_teebox_to_the_rounds()
    {
        var h = await CreateAsync(nameof(RelinkParticipantRoundAsync_forces_the_teebox_to_the_rounds));
        var (gameId, hostPid, _) = await StartedGameAsync(h);

        // The host joined off the Blues but actually played the Whites.
        var round = await StartRoundAsync(h, HostId, h.SecondTeeboxId, h.CourseId);

        Assert.True(await h.Admin.RelinkParticipantRoundAsync(hostPid, round, AdminId));

        var participant = (await h.Admin.GetGameForAdminAsync(gameId))!
            .State.Participants.Single(p => p.ParticipantId == hostPid);

        Assert.Equal(round, participant.RoundId);
        Assert.Equal("White", participant.TeeboxName);
    }

    [Fact]
    public async Task RelinkParticipantRoundAsync_refuses_a_round_belonging_to_another_golfer()
    {
        var h = await CreateAsync(nameof(RelinkParticipantRoundAsync_refuses_a_round_belonging_to_another_golfer));
        var (_, hostPid, _) = await StartedGameAsync(h);

        var friendRound = await StartRoundAsync(h, FriendId, h.TeeboxId, h.CourseId);

        // An admin may fix a mislinked round; they may not attach a stranger's round to
        // somebody else's line.
        Assert.False(await h.Admin.RelinkParticipantRoundAsync(hostPid, friendRound, AdminId));
    }

    [Fact]
    public async Task RelinkParticipantRoundAsync_unlinks_when_given_no_round()
    {
        var h = await CreateAsync(nameof(RelinkParticipantRoundAsync_unlinks_when_given_no_round));
        var (gameId, hostPid, _) = await StartedGameAsync(h);

        var round = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);

        Assert.True(await h.Admin.RelinkParticipantRoundAsync(hostPid, null, AdminId));

        var participant = (await h.Admin.GetGameForAdminAsync(gameId))!
            .State.Participants.Single(p => p.ParticipantId == hostPid);

        Assert.Null(participant.RoundId);
    }

    // ── Names, removal, state ──

    [Fact]
    public async Task RefreshParticipantDisplayNameAsync_resnapshots_from_the_profile()
    {
        var h = await CreateAsync(nameof(RefreshParticipantDisplayNameAsync_resnapshots_from_the_profile));
        var (gameId, hostPid, _) = await StartedGameAsync(h);

        await using (var db = h.Factory.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == HostId);
            user.FirstName = "Dale";
            user.LastName = "Renamed";
            await db.SaveChangesAsync();
        }

        Assert.True(await h.Admin.RefreshParticipantDisplayNameAsync(hostPid, AdminId));

        var participant = (await h.Admin.GetGameForAdminAsync(gameId))!
            .State.Participants.Single(p => p.ParticipantId == hostPid);

        Assert.Equal("Dale Renamed", participant.DisplayName);
    }

    [Fact]
    public async Task RefreshParticipantDisplayNameAsync_leaves_a_guest_alone()
    {
        var h = await CreateAsync(nameof(RefreshParticipantDisplayNameAsync_leaves_a_guest_alone));

        var created = await h.Games.CreateGameAsync(new CreateGameRequest
        {
            GameType = GameType.Skins,
            CourseId = h.CourseId,
            TeeboxId = h.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = true
        }, HostId);

        var added = await h.Games.AddParticipantAsync(created.Value!.GameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        var guestPid = added.Value!.Participants.Single(p => p.IsGuest).ParticipantId;

        // A guest's snapshot is the only name they have — there is no profile to copy.
        Assert.False(await h.Admin.RefreshParticipantDisplayNameAsync(guestPid, AdminId));
    }

    [Fact]
    public async Task RemoveParticipantAsAdminAsync_works_on_a_live_game_unlike_the_api()
    {
        var h = await CreateAsync(nameof(RemoveParticipantAsAdminAsync_works_on_a_live_game_unlike_the_api));
        var (gameId, _, friendPid) = await StartedGameAsync(h);

        // The API refuses this once a game is running; an admin fixing a mistake may do it.
        Assert.Equal(GameResultStatus.GameNotInSetup,
            (await h.Games.RemoveParticipantAsync(gameId, friendPid, HostId)).Status);

        Assert.True(await h.Admin.RemoveParticipantAsAdminAsync(friendPid, AdminId));

        var detail = await h.Admin.GetGameForAdminAsync(gameId);
        Assert.Single(detail!.State.Participants);
    }

    [Fact]
    public async Task SetGameStateAsync_reopens_a_posted_game_so_it_can_be_corrected()
    {
        var h = await CreateAsync(nameof(SetGameStateAsync_reopens_a_posted_game_so_it_can_be_corrected));
        var (gameId, _, _) = await StartedGameAsync(h);
        await h.Games.CompleteGameAsync(gameId, HostId);

        Assert.True(await h.Admin.SetGameStateAsync(gameId, GameState.Active, AdminId));

        var detail = await h.Admin.GetGameForAdminAsync(gameId);
        Assert.Equal(GameState.Active, detail!.State.State);

        // Back to live, so the host can post it again once the data is right.
        Assert.True((await h.Games.CompleteGameAsync(gameId, HostId)).IsOk);
    }

    // ── Re-running scoring ──

    [Fact]
    public async Task RecomputeFinalScoreboardAsync_rebuilds_the_snapshot_from_corrected_data()
    {
        var h = await CreateAsync(nameof(RecomputeFinalScoreboardAsync_rebuilds_the_snapshot_from_corrected_data));

        var (gameId, hostPid, friendPid) = await StartedGameAsync(h, useNet: true);

        var hostRound = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        var friendRound = await StartRoundAsync(h, FriendId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, hostRound, HostId);
        await h.Games.LinkRoundAsync(gameId, friendRound, FriendId);

        await h.Entry.UpsertHoleAsync(hostRound, 1, new UpsertHoleRequest { Score = (short)(Pars[0] + 1) }, HostId);
        await h.Entry.UpsertHoleAsync(friendRound, 1, new UpsertHoleRequest { Score = (short)Pars[0] }, FriendId);

        // Posted with the host's handicap wrong, so the friend is shown as the winner.
        await h.Games.CompleteGameAsync(gameId, HostId);

        var posted = (MatchPlayScoreboard)(await h.Admin.GetGameForAdminAsync(gameId))!.State.Scoreboard!;
        Assert.Equal(friendPid, posted.LeaderParticipantId);

        // Fixing the handicap alone must NOT move a posted game — that is the snapshot doing
        // its job.
        await h.Admin.RepairParticipantAsync(hostPid, new RepairParticipantRequest { CourseHandicap = 1 }, AdminId);

        var stillFrozen = (MatchPlayScoreboard)(await h.Admin.GetGameForAdminAsync(gameId))!.State.Scoreboard!;
        Assert.Equal(friendPid, stillFrozen.LeaderParticipantId);

        // Re-running scoring is the deliberate second step that adopts the correction.
        Assert.True(await h.Admin.RecomputeFinalScoreboardAsync(gameId, AdminId));

        var rebuilt = (MatchPlayScoreboard)(await h.Admin.GetGameForAdminAsync(gameId))!.State.Scoreboard!;
        Assert.True(rebuilt.Holes[0].IsHalved);
        Assert.Null(rebuilt.LeaderParticipantId);
    }

    [Fact]
    public async Task RecomputeFinalScoreboardAsync_refuses_a_game_that_is_not_posted()
    {
        var h = await CreateAsync(nameof(RecomputeFinalScoreboardAsync_refuses_a_game_that_is_not_posted));
        var (gameId, _, _) = await StartedGameAsync(h);

        // A live game's board is derived on every read — there is nothing stored to rebuild.
        Assert.False(await h.Admin.RecomputeFinalScoreboardAsync(gameId, AdminId));
    }

    [Fact]
    public async Task RecomputeFinalScoreboardAsync_writes_a_snapshot_that_reads_back()
    {
        var h = await CreateAsync(nameof(RecomputeFinalScoreboardAsync_writes_a_snapshot_that_reads_back));
        var (gameId, _, _) = await StartedGameAsync(h);
        await h.Games.CompleteGameAsync(gameId, HostId);

        await h.Admin.RecomputeFinalScoreboardAsync(gameId, AdminId);

        await using var db = h.Factory.CreateDbContext();
        var game = await db.Games.SingleAsync(g => g.GameId == gameId);

        // Serialized through the base type, so the polymorphic discriminator survives.
        Assert.Contains("\"gameType\"", game.FinalScoreboard);
        Assert.Equal(AdminId, game.UpdatedBy);

        // And it really does deserialize back into the right concrete board.
        var detail = await h.Admin.GetGameForAdminAsync(gameId);
        Assert.IsType<MatchPlayScoreboard>(detail!.State.Scoreboard);
        Assert.NotNull(detail.FinalScoreboardJson);
    }

    // ── Deleting ──

    [Fact]
    public async Task DeleteGameAsync_removes_the_game_and_its_players_but_leaves_the_rounds()
    {
        var h = await CreateAsync(nameof(DeleteGameAsync_removes_the_game_and_its_players_but_leaves_the_rounds));
        var (gameId, _, _) = await StartedGameAsync(h);

        var round = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);

        Assert.True(await h.Admin.DeleteGameAsync(gameId, AdminId));

        Assert.Null(await h.Admin.GetGameForAdminAsync(gameId));
        Assert.Empty(await h.Admin.GetAllGamesAsync());

        await using var db = h.Factory.CreateDbContext();
        Assert.Empty(await db.GameParticipants.Where(p => p.GameId == gameId && !p.IsDeleted).ToListAsync());

        // The round behind the game is somebody's actual golf — deleting a bet must not take it.
        Assert.False((await db.Rounds.SingleAsync(r => r.RoundId == round)).IsDeleted);
    }
}
