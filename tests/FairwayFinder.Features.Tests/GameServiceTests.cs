using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// Games end to end through the real service, seeded the way <c>RoundEntryServiceTests</c> seeds:
/// a course, teeboxes and holes by hand, everything else driven through the services that own it.
///
/// One path cannot be covered here — the unique-violation retries in the guest hole upsert and in
/// join-code generation. The in-memory provider ignores filtered unique indexes entirely, so those
/// need a look against real Postgres.
/// </summary>
public class GameServiceTests
{
    private const string HostId = "host-user";
    private const string FriendId = "friend-user";
    private const string StrangerId = "stranger-user";

    private static readonly int[] Pars = [4, 5, 3, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 4, 5, 4];

    private sealed record Harness(
        GameService Games,
        RoundEntryService Entry,
        InMemoryDbContextFactory Factory,
        CountingPushNotificationService Push,
        long CourseId,
        long TeeboxId,
        long NineHoleTeeboxId,
        long SecondTeeboxId,
        long OtherCourseId,
        long OtherCourseTeeboxId,
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

        await using var db = factory.CreateDbContext();

        var course = new Course { CourseName = "Test Course", CreatedBy = HostId, UpdatedBy = HostId };
        var otherCourse = new Course { CourseName = "Other Course", CreatedBy = HostId, UpdatedBy = HostId };
        db.Courses.AddRange(course, otherCourse);
        await db.SaveChangesAsync();

        var teebox = NewTeebox(course.CourseId, "Blue", 18);
        var second = NewTeebox(course.CourseId, "White", 18);
        var nineHole = NewTeebox(course.CourseId, "Blue Nine", 9, isNineHole: true);
        var archived = NewTeebox(course.CourseId, "Old Blue", 18);
        archived.ArchivedOn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        archived.ArchivedBy = HostId;
        var otherTeebox = NewTeebox(otherCourse.CourseId, "Green", 18);

        db.Teeboxes.AddRange(teebox, second, nineHole, archived, otherTeebox);
        await db.SaveChangesAsync();

        // Every teebox gets its own holes — holes hang off the teebox, not the course.
        AddHoles(db, teebox, 18);
        AddHoles(db, second, 18);
        AddHoles(db, nineHole, 9);
        AddHoles(db, archived, 18);
        AddHoles(db, otherTeebox, 18);

        db.Users.AddRange(
            NewUser(HostId, "Dale", "Host"),
            NewUser(FriendId, "Sam", "Friend"),
            NewUser(StrangerId, "Kit", "Stranger"));

        await db.SaveChangesAsync();

        return new Harness(games, entry, factory, push, course.CourseId, teebox.TeeboxId,
            nineHole.TeeboxId, second.TeeboxId, otherCourse.CourseId, otherTeebox.TeeboxId, archived.TeeboxId);
    }

    private static Teebox NewTeebox(long courseId, string name, int holeCount, bool isNineHole = false) => new()
    {
        CourseId = courseId,
        TeeboxName = name,
        Par = Pars.Take(holeCount).Sum(),
        Rating = 71.5m,
        Slope = 130,
        IsNineHole = isNineHole,
        CreatedBy = HostId,
        UpdatedBy = HostId
    };

    private static void AddHoles(FairwayFinder.Data.ApplicationDbContext db, Teebox teebox, int holeCount)
        => db.Holes.AddRange(Enumerable.Range(1, holeCount).Select(n => new Hole
        {
            TeeboxId = teebox.TeeboxId,
            CourseId = teebox.CourseId,
            HoleNumber = n,
            Par = Pars[n - 1],
            Yardage = 400,
            Handicap = n,
            CreatedBy = HostId,
            UpdatedBy = HostId
        }));

    private static FairwayFinder.Identity.ApplicationUser NewUser(string id, string first, string last) => new()
    {
        Id = id,
        UserName = $"{id}@test.com",
        Email = $"{id}@test.com",
        FirstName = first,
        LastName = last
    };

    private static CreateGameRequest CreateRequest(
        Harness h,
        GameType gameType = GameType.MatchPlay,
        long? teeboxId = null,
        bool fullRound = true,
        bool frontNine = false,
        bool backNine = false,
        int courseHandicap = 0,
        bool useNet = false,
        long? roundId = null,
        int? team = null,
        long? courseId = null) => new()
    {
        GameType = gameType,
        CourseId = courseId ?? h.CourseId,
        TeeboxId = teeboxId ?? h.TeeboxId,
        DatePlayed = new DateOnly(2026, 7, 1),
        FullRound = fullRound,
        FrontNine = frontNine,
        BackNine = backNine,
        CourseHandicap = courseHandicap,
        UseNet = useNet,
        RoundId = roundId,
        Team = team
    };

    /// <summary>A two-player match with the friend joined, still in setup.</summary>
    private static async Task<(long GameId, long HostParticipantId, long FriendParticipantId)> TwoPlayerGameAsync(
        Harness h, GameType gameType = GameType.MatchPlay)
    {
        var created = await h.Games.CreateGameAsync(CreateRequest(h, gameType), HostId);
        Assert.True(created.IsOk);

        var gameId = created.Value!.GameId;

        var joined = await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = created.Value.JoinCode, TeeboxId = h.TeeboxId }, FriendId);
        Assert.True(joined.IsOk);

        var host = joined.Value!.Participants.Single(p => p.UserId == HostId);
        var friend = joined.Value.Participants.Single(p => p.UserId == FriendId);

        return (gameId, host.ParticipantId, friend.ParticipantId);
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

        Assert.True(started.IsOk);
        return started.Value!.RoundId;
    }

    private static async Task PlayHoleAsync(Harness h, long roundId, string userId, int holeNumber, short score)
    {
        var result = await h.Entry.UpsertHoleAsync(roundId, holeNumber, new UpsertHoleRequest { Score = score }, userId);
        Assert.True(result.IsOk);
    }

    // ── Create ──

    [Fact]
    public async Task CreateGameAsync_makes_the_host_the_first_participant()
    {
        var h = await CreateAsync(nameof(CreateGameAsync_makes_the_host_the_first_participant));

        var result = await h.Games.CreateGameAsync(CreateRequest(h, courseHandicap: 12), HostId);

        Assert.True(result.IsOk);
        var participant = Assert.Single(result.Value!.Participants);
        Assert.Equal(HostId, participant.UserId);
        Assert.True(participant.IsHost);
        Assert.False(participant.IsGuest);
        Assert.Equal("Dale Host", participant.DisplayName);
        Assert.Equal(12, participant.CourseHandicap);
        Assert.Equal(GameState.Setup, result.Value.State);
        Assert.Equal(18, result.Value.HoleNumbers.Count);
        Assert.NotEmpty(result.Value.JoinCode);
    }

    [Fact]
    public async Task CreateGameAsync_leaves_the_scoreboard_null_while_the_game_is_in_setup()
    {
        var h = await CreateAsync(nameof(CreateGameAsync_leaves_the_scoreboard_null_while_the_game_is_in_setup));

        var result = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        // Match play with one participant has no two sides to score. Asking the engine here
        // would be asking a question that has no answer.
        Assert.Null(result.Value!.Scoreboard);
    }

    [Fact]
    public async Task CreateGameAsync_refuses_an_archived_teebox()
    {
        var h = await CreateAsync(nameof(CreateGameAsync_refuses_an_archived_teebox));

        var result = await h.Games.CreateGameAsync(CreateRequest(h, teeboxId: h.ArchivedTeeboxId), HostId);

        Assert.Equal(GameResultStatus.TeeboxArchived, result.Status);
    }

    [Fact]
    public async Task CreateGameAsync_refuses_a_teebox_on_another_course()
    {
        var h = await CreateAsync(nameof(CreateGameAsync_refuses_a_teebox_on_another_course));

        var result = await h.Games.CreateGameAsync(CreateRequest(h, teeboxId: h.OtherCourseTeeboxId), HostId);

        Assert.Equal(GameResultStatus.TeeboxNotOnCourse, result.Status);
    }

    [Fact]
    public async Task CreateGameAsync_refuses_a_teebox_that_does_not_cover_the_games_holes()
    {
        var h = await CreateAsync(nameof(CreateGameAsync_refuses_a_teebox_that_does_not_cover_the_games_holes));

        // A nine-hole teebox cannot host an eighteen-hole game.
        var result = await h.Games.CreateGameAsync(CreateRequest(h, teeboxId: h.NineHoleTeeboxId), HostId);

        Assert.Equal(GameResultStatus.TeeboxShapeMismatch, result.Status);
        Assert.Contains("10", result.Detail);
    }

    [Fact]
    public async Task CreateGameAsync_accepts_a_nine_hole_teebox_for_a_front_nine_game()
    {
        var h = await CreateAsync(nameof(CreateGameAsync_accepts_a_nine_hole_teebox_for_a_front_nine_game));

        var result = await h.Games.CreateGameAsync(
            CreateRequest(h, teeboxId: h.NineHoleTeeboxId, fullRound: false, frontNine: true), HostId);

        Assert.True(result.IsOk);
        Assert.Equal(9, result.Value!.HoleNumbers.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], result.Value.HoleNumbers);
    }

    [Fact]
    public async Task CreateGameAsync_derives_a_back_nine_hole_set()
    {
        var h = await CreateAsync(nameof(CreateGameAsync_derives_a_back_nine_hole_set));

        var result = await h.Games.CreateGameAsync(
            CreateRequest(h, fullRound: false, backNine: true), HostId);

        Assert.True(result.IsOk);
        Assert.Equal([10, 11, 12, 13, 14, 15, 16, 17, 18], result.Value!.HoleNumbers);
    }

    // ── Joining and the field ──

    [Fact]
    public async Task JoinGameAsync_matches_the_join_code_case_insensitively()
    {
        var h = await CreateAsync(nameof(JoinGameAsync_matches_the_join_code_case_insensitively));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        var result = await h.Games.JoinGameAsync(new JoinGameRequest
        {
            JoinCode = created.Value!.JoinCode.ToLowerInvariant(),
            TeeboxId = h.TeeboxId
        }, FriendId);

        Assert.True(result.IsOk);
        Assert.Equal(2, result.Value!.Participants.Count);
    }

    [Fact]
    public async Task JoinGameAsync_refuses_an_unknown_code()
    {
        var h = await CreateAsync(nameof(JoinGameAsync_refuses_an_unknown_code));

        var result = await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = "ZZZZZZ", TeeboxId = h.TeeboxId }, FriendId);

        Assert.Equal(GameResultStatus.JoinCodeInvalid, result.Status);
    }

    [Fact]
    public async Task JoinGameAsync_refuses_a_code_for_a_completed_game()
    {
        var h = await CreateAsync(nameof(JoinGameAsync_refuses_a_code_for_a_completed_game));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        var code = (await h.Games.GetGameAsync(gameId, HostId)).Value!.JoinCode;
        await h.Games.StartGameAsync(gameId, HostId);
        await h.Games.CompleteGameAsync(gameId, HostId);

        // The unique index only covers live games, so the lookup has to filter on state too.
        var result = await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, StrangerId);

        Assert.Equal(GameResultStatus.JoinCodeInvalid, result.Status);
    }

    [Fact]
    public async Task JoinGameAsync_refuses_to_add_the_same_golfer_twice()
    {
        var h = await CreateAsync(nameof(JoinGameAsync_refuses_to_add_the_same_golfer_twice));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);
        var code = created.Value!.JoinCode;

        await h.Games.JoinGameAsync(new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, FriendId);
        var again = await h.Games.JoinGameAsync(new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, FriendId);

        Assert.Equal(GameResultStatus.AlreadyJoined, again.Status);
    }

    [Fact]
    public async Task AddParticipantAsync_refuses_a_user_who_is_not_a_friend()
    {
        var h = await CreateAsync(nameof(AddParticipantAsync_refuses_a_user_who_is_not_a_friend));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        var result = await h.Games.AddParticipantAsync(created.Value!.GameId, new AddParticipantRequest
        {
            UserId = StrangerId,
            TeeboxId = h.TeeboxId
        }, HostId);

        Assert.Equal(GameResultStatus.NotFriends, result.Status);
    }

    [Fact]
    public async Task AddParticipantAsync_accepts_a_friend()
    {
        var h = await CreateAsync(nameof(AddParticipantAsync_accepts_a_friend));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        var result = await h.Games.AddParticipantAsync(created.Value!.GameId, new AddParticipantRequest
        {
            UserId = FriendId,
            TeeboxId = h.TeeboxId
        }, HostId);

        Assert.True(result.IsOk);
        Assert.Equal("Sam Friend", result.Value!.Participants.Single(p => p.UserId == FriendId).DisplayName);
    }

    [Fact]
    public async Task AddParticipantAsync_accepts_a_guest_with_no_user_id()
    {
        var h = await CreateAsync(nameof(AddParticipantAsync_accepts_a_guest_with_no_user_id));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        var result = await h.Games.AddParticipantAsync(created.Value!.GameId, new AddParticipantRequest
        {
            DisplayName = "Walk-on Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        Assert.True(result.IsOk);
        var guest = result.Value!.Participants.Single(p => p.IsGuest);
        Assert.Equal("Walk-on Wally", guest.DisplayName);
        Assert.Null(guest.UserId);
    }

    [Fact]
    public async Task AddParticipantAsync_refuses_a_caller_who_is_not_the_host()
    {
        var h = await CreateAsync(nameof(AddParticipantAsync_refuses_a_caller_who_is_not_the_host));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        var result = await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Guest",
            TeeboxId = h.TeeboxId
        }, FriendId);

        Assert.Equal(GameResultStatus.NotHost, result.Status);
    }

    [Fact]
    public async Task RemoveParticipantAsync_refuses_once_the_game_is_running()
    {
        var h = await CreateAsync(nameof(RemoveParticipantAsync_refuses_once_the_game_is_running));
        var (gameId, _, friendParticipantId) = await TwoPlayerGameAsync(h);
        await h.Games.StartGameAsync(gameId, HostId);

        var result = await h.Games.RemoveParticipantAsync(gameId, friendParticipantId, HostId);

        Assert.Equal(GameResultStatus.GameNotInSetup, result.Status);
    }

    // ── Linking rounds ──

    [Fact]
    public async Task LinkRoundAsync_refuses_a_round_owned_by_someone_else()
    {
        var h = await CreateAsync(nameof(LinkRoundAsync_refuses_a_round_owned_by_someone_else));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        var friendRound = await StartRoundAsync(h, FriendId, h.TeeboxId, h.CourseId);

        var result = await h.Games.LinkRoundAsync(gameId, friendRound, HostId);

        Assert.Equal(GameResultStatus.RoundNotOwned, result.Status);
    }

    [Fact]
    public async Task LinkRoundAsync_refuses_a_round_on_another_course()
    {
        var h = await CreateAsync(nameof(LinkRoundAsync_refuses_a_round_on_another_course));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        var elsewhere = await StartRoundAsync(h, HostId, h.OtherCourseTeeboxId, h.OtherCourseId);

        var result = await h.Games.LinkRoundAsync(gameId, elsewhere, HostId);

        Assert.Equal(GameResultStatus.RoundNotOnGameCourse, result.Status);
    }

    [Fact]
    public async Task LinkRoundAsync_forces_the_participant_teebox_to_the_rounds_teebox()
    {
        var h = await CreateAsync(nameof(LinkRoundAsync_forces_the_participant_teebox_to_the_rounds_teebox));

        // The host joins off the Blues but links a round played off the Whites. The round's
        // scores join to holes on its own teebox, so that teebox has to win — otherwise par and
        // stroke index would be read off a different hole set than the strokes came from.
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);
        var gameId = created.Value!.GameId;

        await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = created.Value.JoinCode, TeeboxId = h.TeeboxId }, FriendId);

        var round = await StartRoundAsync(h, HostId, h.SecondTeeboxId, h.CourseId);
        var result = await h.Games.LinkRoundAsync(gameId, round, HostId);

        Assert.True(result.IsOk);
        var host = result.Value!.Participants.Single(p => p.UserId == HostId);
        Assert.Equal(h.SecondTeeboxId, host.TeeboxId);
        Assert.Equal("White", host.TeeboxName);
        Assert.Equal(round, host.RoundId);
    }

    [Fact]
    public async Task LinkRoundAsync_with_a_null_round_id_unlinks()
    {
        var h = await CreateAsync(nameof(LinkRoundAsync_with_a_null_round_id_unlinks));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        var round = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);

        await h.Games.LinkRoundAsync(gameId, round, HostId);
        var result = await h.Games.LinkRoundAsync(gameId, roundId: null, HostId);

        Assert.True(result.IsOk);
        Assert.Null(result.Value!.Participants.Single(p => p.UserId == HostId).RoundId);
    }

    [Fact]
    public async Task GetGameAsync_marks_a_participant_unavailable_when_their_round_is_deleted()
    {
        var h = await CreateAsync(nameof(GetGameAsync_marks_a_participant_unavailable_when_their_round_is_deleted));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        var round = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);

        await using (var db = h.Factory.CreateDbContext())
        {
            var row = await db.Rounds.SingleAsync(r => r.RoundId == round);
            row.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        var result = await h.Games.GetGameAsync(gameId, HostId);

        Assert.True(result.Value!.Participants.Single(p => p.UserId == HostId).RoundUnavailable);
    }

    [Fact]
    public async Task GetGameAsync_does_not_mark_a_freshly_started_round_unavailable()
    {
        var h = await CreateAsync(nameof(GetGameAsync_does_not_mark_a_freshly_started_round_unavailable));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        var round = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);

        var result = await h.Games.LinkRoundAsync(gameId, round, HostId);

        // No scores yet, but the round exists — the app must not prompt to relink on the first tee.
        var host = result.Value!.Participants.Single(p => p.UserId == HostId);
        Assert.False(host.RoundUnavailable);
        Assert.Equal(0, host.HolesEntered);
    }

    // ── Scoring through the round flow ──

    [Fact]
    public async Task GetGameAsync_moves_the_match_as_holes_are_written_through_the_round_service()
    {
        var h = await CreateAsync(nameof(GetGameAsync_moves_the_match_as_holes_are_written_through_the_round_service));
        var (gameId, hostParticipantId, _) = await TwoPlayerGameAsync(h);

        var hostRound = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        var friendRound = await StartRoundAsync(h, FriendId, h.TeeboxId, h.CourseId);

        await h.Games.LinkRoundAsync(gameId, hostRound, HostId);
        await h.Games.LinkRoundAsync(gameId, friendRound, FriendId);
        await h.Games.StartGameAsync(gameId, HostId);

        // Host wins the first two holes.
        for (var n = 1; n <= 2; n++)
        {
            await PlayHoleAsync(h, hostRound, HostId, n, (short)Pars[n - 1]);
            await PlayHoleAsync(h, friendRound, FriendId, n, (short)(Pars[n - 1] + 1));
        }

        var result = await h.Games.GetGameAsync(gameId, HostId);

        var board = Assert.IsType<MatchPlayScoreboard>(result.Value!.Scoreboard);
        Assert.Equal(2, board.HolesPlayed);
        Assert.Equal(16, board.HolesRemaining);
        Assert.Equal(2, board.HolesUp);
        Assert.Equal(hostParticipantId, board.LeaderParticipantId);
    }

    [Fact]
    public async Task GetGameAsync_scores_a_guest_from_host_entered_strokes()
    {
        var h = await CreateAsync(nameof(GetGameAsync_scores_a_guest_from_host_entered_strokes));

        var created = await h.Games.CreateGameAsync(CreateRequest(h, GameType.Skins), HostId);
        var gameId = created.Value!.GameId;

        var added = await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        var hostParticipantId = added.Value!.Participants.Single(p => p.UserId == HostId).ParticipantId;
        var guestId = added.Value.Participants.Single(p => p.IsGuest).ParticipantId;

        var hostRound = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, hostRound, HostId);
        await h.Games.StartGameAsync(gameId, HostId);

        // Host makes par on 1, the guest makes bogey — a skin to the host.
        await PlayHoleAsync(h, hostRound, HostId, 1, (short)Pars[0]);
        var result = await h.Games.UpsertParticipantHoleAsync(
            gameId, guestId, 1, new UpsertGameHoleRequest { Strokes = (short)(Pars[0] + 1) }, HostId);

        Assert.True(result.IsOk);
        var board = Assert.IsType<SkinsScoreboard>(result.Value!.Scoreboard);
        Assert.Equal(1, board.HolesPlayed);
        Assert.Equal(hostParticipantId, board.Holes[0].WonByParticipantId);
        Assert.Equal(1, board.Tallies.Single(t => t.ParticipantId == hostParticipantId).SkinsWon);
    }

    [Fact]
    public async Task UpsertParticipantHoleAsync_refuses_a_participant_who_has_linked_a_round()
    {
        var h = await CreateAsync(nameof(UpsertParticipantHoleAsync_refuses_a_participant_who_has_linked_a_round));
        var (gameId, hostParticipantId, _) = await TwoPlayerGameAsync(h);
        var round = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);
        await h.Games.StartGameAsync(gameId, HostId);

        // Without this guard the write succeeds and is never read — the round is the source of truth.
        var result = await h.Games.UpsertParticipantHoleAsync(
            gameId, hostParticipantId, 1, new UpsertGameHoleRequest { Strokes = 4 }, HostId);

        Assert.Equal(GameResultStatus.ParticipantUsesLinkedRound, result.Status);
    }

    [Fact]
    public async Task UpsertParticipantHoleAsync_refuses_a_hole_the_game_does_not_play()
    {
        var h = await CreateAsync(nameof(UpsertParticipantHoleAsync_refuses_a_hole_the_game_does_not_play));

        var created = await h.Games.CreateGameAsync(
            CreateRequest(h, GameType.Skins, teeboxId: h.NineHoleTeeboxId, fullRound: false, frontNine: true), HostId);
        var gameId = created.Value!.GameId;

        var added = await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.NineHoleTeeboxId
        }, HostId);

        var guestId = added.Value!.Participants.Single(p => p.IsGuest).ParticipantId;

        var result = await h.Games.UpsertParticipantHoleAsync(
            gameId, guestId, 12, new UpsertGameHoleRequest { Strokes = 4 }, HostId);

        Assert.Equal(GameResultStatus.HoleNotInGame, result.Status);
    }

    [Fact]
    public async Task UpsertParticipantHoleAsync_is_idempotent_when_the_same_hole_is_written_twice()
    {
        var h = await CreateAsync(nameof(UpsertParticipantHoleAsync_is_idempotent_when_the_same_hole_is_written_twice));
        var (gameId, guestId) = await SkinsGameWithGuestAsync(h);

        await h.Games.UpsertParticipantHoleAsync(gameId, guestId, 1, new UpsertGameHoleRequest { Strokes = 6 }, HostId);
        var second = await h.Games.UpsertParticipantHoleAsync(gameId, guestId, 1, new UpsertGameHoleRequest { Strokes = 5 }, HostId);

        Assert.True(second.IsOk);

        await using var db = h.Factory.CreateDbContext();
        var rows = await db.GameHoleScores
            .Where(s => s.GameParticipantId == guestId && s.HoleNumber == 1 && !s.IsDeleted)
            .ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal((short)5, row.Strokes);
    }

    [Fact]
    public async Task ClearParticipantHoleAsync_takes_the_hole_back_out_of_the_scoreboard()
    {
        var h = await CreateAsync(nameof(ClearParticipantHoleAsync_takes_the_hole_back_out_of_the_scoreboard));
        var (gameId, guestId) = await SkinsGameWithGuestAsync(h);

        await h.Games.UpsertParticipantHoleAsync(gameId, guestId, 1, new UpsertGameHoleRequest { Strokes = 5 }, HostId);
        var cleared = await h.Games.ClearParticipantHoleAsync(gameId, guestId, 1, HostId);

        Assert.True(cleared.IsOk);
        Assert.Equal(0, cleared.Value!.Participants.Single(p => p.ParticipantId == guestId).HolesEntered);
    }

    [Fact]
    public async Task UpsertParticipantHoleAsync_refuses_a_stranger()
    {
        var h = await CreateAsync(nameof(UpsertParticipantHoleAsync_refuses_a_stranger));
        var (gameId, guestId) = await SkinsGameWithGuestAsync(h);

        var result = await h.Games.UpsertParticipantHoleAsync(
            gameId, guestId, 1, new UpsertGameHoleRequest { Strokes = 5 }, StrangerId);

        Assert.Equal(GameResultStatus.NotHost, result.Status);
    }

    /// <summary>A started skins game with the host on a round and one guest to score by hand.</summary>
    private static async Task<(long GameId, long GuestParticipantId)> SkinsGameWithGuestAsync(Harness h)
    {
        var created = await h.Games.CreateGameAsync(CreateRequest(h, GameType.Skins), HostId);
        var gameId = created.Value!.GameId;

        var added = await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        var guestId = added.Value!.Participants.Single(p => p.IsGuest).ParticipantId;

        var round = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);
        await h.Games.StartGameAsync(gameId, HostId);

        return (gameId, guestId);
    }

    // ── Authorization ──

    [Fact]
    public async Task GetGameAsync_refuses_a_caller_who_is_not_a_participant()
    {
        var h = await CreateAsync(nameof(GetGameAsync_refuses_a_caller_who_is_not_a_participant));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        var result = await h.Games.GetGameAsync(gameId, StrangerId);

        Assert.Equal(GameResultStatus.NotParticipant, result.Status);
    }

    [Fact]
    public async Task GetGameAsync_lets_a_trusted_caller_read_across_users()
    {
        var h = await CreateAsync(nameof(GetGameAsync_lets_a_trusted_caller_read_across_users));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        // Null means the admin console, which reads every user's games by design.
        var result = await h.Games.GetGameAsync(gameId, userId: null);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task GetMyGamesAsync_returns_games_where_the_caller_is_a_participant()
    {
        var h = await CreateAsync(nameof(GetMyGamesAsync_returns_games_where_the_caller_is_a_participant));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        var mine = await h.Games.GetMyGamesAsync(FriendId, activeOnly: false);
        var theirs = await h.Games.GetMyGamesAsync(StrangerId, activeOnly: false);

        var game = Assert.Single(mine);
        Assert.Equal(gameId, game.GameId);
        Assert.False(game.IsHost);
        Assert.Equal(2, game.ParticipantCount);
        Assert.Empty(theirs);
    }

    [Fact]
    public async Task GetMyGamesAsync_can_filter_to_live_games()
    {
        var h = await CreateAsync(nameof(GetMyGamesAsync_can_filter_to_live_games));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        await h.Games.StartGameAsync(gameId, HostId);
        await h.Games.CompleteGameAsync(gameId, HostId);

        Assert.Empty(await h.Games.GetMyGamesAsync(HostId, activeOnly: true));
        Assert.Single(await h.Games.GetMyGamesAsync(HostId, activeOnly: false));
    }

    // ── Lifecycle ──

    [Fact]
    public async Task StartGameAsync_refuses_a_match_play_game_with_three_sides()
    {
        var h = await CreateAsync(nameof(StartGameAsync_refuses_a_match_play_game_with_three_sides));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        var result = await h.Games.StartGameAsync(gameId, HostId);

        Assert.Equal(GameResultStatus.ParticipantCountInvalid, result.Status);
        Assert.Contains("two sides", result.Detail);
    }

    [Fact]
    public async Task StartGameAsync_refuses_a_skins_game_with_teams()
    {
        var h = await CreateAsync(nameof(StartGameAsync_refuses_a_skins_game_with_teams));

        var created = await h.Games.CreateGameAsync(CreateRequest(h, GameType.Skins, team: 1), HostId);
        var gameId = created.Value!.GameId;

        await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId,
            Team = 2
        }, HostId);

        var result = await h.Games.StartGameAsync(gameId, HostId);

        Assert.Equal(GameResultStatus.ParticipantCountInvalid, result.Status);
        Assert.Contains("individual game", result.Detail);
    }

    [Fact]
    public async Task StartGameAsync_refuses_a_game_with_one_player()
    {
        var h = await CreateAsync(nameof(StartGameAsync_refuses_a_game_with_one_player));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        var result = await h.Games.StartGameAsync(created.Value!.GameId, HostId);

        Assert.Equal(GameResultStatus.ParticipantCountInvalid, result.Status);
    }

    [Fact]
    public async Task StartGameAsync_refuses_a_caller_who_is_not_the_host()
    {
        var h = await CreateAsync(nameof(StartGameAsync_refuses_a_caller_who_is_not_the_host));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        var result = await h.Games.StartGameAsync(gameId, FriendId);

        Assert.Equal(GameResultStatus.NotHost, result.Status);
    }

    [Fact]
    public async Task StartGameAsync_notifies_everyone_but_the_host()
    {
        var h = await CreateAsync(nameof(StartGameAsync_notifies_everyone_but_the_host));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        await h.Games.StartGameAsync(gameId, HostId);

        var sent = Assert.Single(h.Push.Sent);
        Assert.Equal(FriendId, sent.UserId);
    }

    [Fact]
    public async Task CompleteGameAsync_snapshots_the_scoreboard()
    {
        var h = await CreateAsync(nameof(CompleteGameAsync_snapshots_the_scoreboard));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        await h.Games.StartGameAsync(gameId, HostId);

        var result = await h.Games.CompleteGameAsync(gameId, HostId);

        Assert.True(result.IsOk);
        Assert.Equal(GameState.Completed, result.Value!.State);

        await using var db = h.Factory.CreateDbContext();
        var game = await db.Games.SingleAsync(g => g.GameId == gameId);
        Assert.False(string.IsNullOrWhiteSpace(game.FinalScoreboard));

        // Serialized through the base type, so the discriminator survives and it reads back.
        Assert.Contains("\"gameType\"", game.FinalScoreboard);
        Assert.Contains("MatchPlay", game.FinalScoreboard);
    }

    [Fact]
    public async Task CompleteGameAsync_returns_the_snapshot_rather_than_a_recompute_after_a_round_is_edited()
    {
        var h = await CreateAsync(nameof(CompleteGameAsync_returns_the_snapshot_rather_than_a_recompute_after_a_round_is_edited));
        var (gameId, hostParticipantId, _) = await TwoPlayerGameAsync(h);

        var hostRound = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        var friendRound = await StartRoundAsync(h, FriendId, h.TeeboxId, h.CourseId);

        await h.Games.LinkRoundAsync(gameId, hostRound, HostId);
        await h.Games.LinkRoundAsync(gameId, friendRound, FriendId);
        await h.Games.StartGameAsync(gameId, HostId);

        // Host wins hole 1 and the game is posted on that.
        await PlayHoleAsync(h, hostRound, HostId, 1, (short)Pars[0]);
        await PlayHoleAsync(h, friendRound, FriendId, 1, (short)(Pars[0] + 1));

        var completed = await h.Games.CompleteGameAsync(gameId, HostId);
        var settled = Assert.IsType<MatchPlayScoreboard>(completed.Value!.Scoreboard);
        Assert.Equal(1, settled.HolesUp);
        Assert.Equal(hostParticipantId, settled.LeaderParticipantId);

        // The loser later edits their round. Rounds stay editable on purpose — but a settled bet
        // must not move months later, which is the entire reason FinalScoreboard exists.
        await PlayHoleAsync(h, friendRound, FriendId, 1, (short)(Pars[0] - 2));

        var afterEdit = await h.Games.GetGameAsync(gameId, HostId);
        var board = Assert.IsType<MatchPlayScoreboard>(afterEdit.Value!.Scoreboard);

        Assert.Equal(1, board.HolesUp);
        Assert.Equal(hostParticipantId, board.LeaderParticipantId);
    }

    [Fact]
    public async Task CompleteGameAsync_refuses_a_game_that_never_started()
    {
        var h = await CreateAsync(nameof(CompleteGameAsync_refuses_a_game_that_never_started));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);

        var result = await h.Games.CompleteGameAsync(gameId, HostId);

        Assert.Equal(GameResultStatus.GameNotActive, result.Status);
    }

    [Fact]
    public async Task CompleteGameAsync_refuses_a_second_time()
    {
        var h = await CreateAsync(nameof(CompleteGameAsync_refuses_a_second_time));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        await h.Games.StartGameAsync(gameId, HostId);
        await h.Games.CompleteGameAsync(gameId, HostId);

        var again = await h.Games.CompleteGameAsync(gameId, HostId);

        Assert.Equal(GameResultStatus.GameAlreadyComplete, again.Status);
    }

    [Fact]
    public async Task AbandonGameAsync_leaves_the_game_unscoreable()
    {
        var h = await CreateAsync(nameof(AbandonGameAsync_leaves_the_game_unscoreable));
        var (gameId, _, _) = await TwoPlayerGameAsync(h);
        await h.Games.StartGameAsync(gameId, HostId);

        var result = await h.Games.AbandonGameAsync(gameId, HostId);

        Assert.True(result.IsOk);
        Assert.Equal(GameState.Abandoned, result.Value!.State);
    }

    // ── Handicaps ──

    [Fact]
    public async Task GetGameAsync_reports_the_playing_handicap_off_the_low_player()
    {
        var h = await CreateAsync(nameof(GetGameAsync_reports_the_playing_handicap_off_the_low_player));

        var created = await h.Games.CreateGameAsync(CreateRequest(h, courseHandicap: 12), HostId);
        var result = await h.Games.JoinGameAsync(new JoinGameRequest
        {
            JoinCode = created.Value!.JoinCode,
            TeeboxId = h.TeeboxId,
            CourseHandicap = 4
        }, FriendId);

        var host = result.Value!.Participants.Single(p => p.UserId == HostId);
        var friend = result.Value.Participants.Single(p => p.UserId == FriendId);

        Assert.Equal(12, host.CourseHandicap);
        Assert.Equal(8, host.PlayingHandicap);
        Assert.Equal(0, friend.PlayingHandicap);
    }

    [Fact]
    public async Task GetGameAsync_applies_strokes_to_the_net_result()
    {
        var h = await CreateAsync(nameof(GetGameAsync_applies_strokes_to_the_net_result));

        // Host gets a shot on hole 1 (stroke index 1). Gross bogey becomes a net par, halving
        // the hole against the friend's par.
        var created = await h.Games.CreateGameAsync(
            CreateRequest(h, courseHandicap: 1, useNet: true), HostId);
        var gameId = created.Value!.GameId;

        await h.Games.JoinGameAsync(new JoinGameRequest
        {
            JoinCode = created.Value.JoinCode,
            TeeboxId = h.TeeboxId,
            CourseHandicap = 0
        }, FriendId);

        var hostRound = await StartRoundAsync(h, HostId, h.TeeboxId, h.CourseId);
        var friendRound = await StartRoundAsync(h, FriendId, h.TeeboxId, h.CourseId);
        await h.Games.LinkRoundAsync(gameId, hostRound, HostId);
        await h.Games.LinkRoundAsync(gameId, friendRound, FriendId);
        await h.Games.StartGameAsync(gameId, HostId);

        await PlayHoleAsync(h, hostRound, HostId, 1, (short)(Pars[0] + 1));
        await PlayHoleAsync(h, friendRound, FriendId, 1, (short)Pars[0]);

        var result = await h.Games.GetGameAsync(gameId, HostId);
        var board = Assert.IsType<MatchPlayScoreboard>(result.Value!.Scoreboard);

        Assert.True(board.Holes[0].IsHalved);
    }
}
