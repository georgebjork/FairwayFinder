using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// The join-code preview and the per-participant hole list — the two additions the iOS client
/// needed so it can show what a code resolves to before committing to it, and render what has
/// already been entered instead of blank steppers.
/// </summary>
public class GamePreviewAndHolesTests
{
    private const string HostId = "host-user";
    private const string FriendId = "friend-user";
    private const string StrangerId = "stranger-user";

    private static readonly int[] Pars = [4, 5, 3, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 4, 5, 4];

    private sealed record Harness(
        GameService Games,
        RoundEntryService Entry,
        InMemoryDbContextFactory Factory,
        long CourseId,
        long TeeboxId,
        long NineHoleTeeboxId);

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

        var course = new Course { CourseName = "Preview Links", CreatedBy = HostId, UpdatedBy = HostId };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var blue = NewTeebox(course.CourseId, "Blue", 18);
        var nine = NewTeebox(course.CourseId, "Blue Nine", 9, isNineHole: true);
        db.Teeboxes.AddRange(blue, nine);
        await db.SaveChangesAsync();

        AddHoles(db, blue, 18);
        AddHoles(db, nine, 9);

        db.Users.AddRange(
            NewUser(HostId, "Dale", "Host"),
            NewUser(FriendId, "Sam", "Friend"),
            NewUser(StrangerId, "Kit", "Stranger"));

        await db.SaveChangesAsync();

        return new Harness(games, entry, factory, course.CourseId, blue.TeeboxId, nine.TeeboxId);
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
        Harness h, GameType gameType = GameType.MatchPlay, long? teeboxId = null,
        bool fullRound = true, bool frontNine = false) => new()
    {
        GameType = gameType,
        CourseId = h.CourseId,
        TeeboxId = teeboxId ?? h.TeeboxId,
        DatePlayed = new DateOnly(2026, 7, 1),
        FullRound = fullRound,
        FrontNine = frontNine
    };

    private static async Task<long> StartRoundAsync(Harness h, string userId, long teeboxId)
    {
        var started = await h.Entry.StartRoundAsync(new StartRoundRequest
        {
            CourseId = h.CourseId,
            TeeboxId = teeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = true
        }, userId);

        return started.Value!.RoundId;
    }

    // ── Preview ──

    [Fact]
    public async Task PreviewGameAsync_resolves_a_code_to_the_game_it_names()
    {
        var h = await CreateAsync(nameof(PreviewGameAsync_resolves_a_code_to_the_game_it_names));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        var preview = await h.Games.PreviewGameAsync(created.Value!.JoinCode, StrangerId);

        Assert.True(preview.IsOk);
        Assert.Equal(created.Value.GameId, preview.Value!.GameId);
        Assert.Equal(GameType.MatchPlay, preview.Value.GameType);
        Assert.Equal(GameState.Setup, preview.Value.State);
        Assert.Equal(h.CourseId, preview.Value.CourseId);
        Assert.Equal("Preview Links", preview.Value.CourseName);
        Assert.Equal("Dale Host", preview.Value.HostDisplayName);
        Assert.Equal(1, preview.Value.ParticipantCount);
        Assert.Equal(18, preview.Value.HoleNumbers.Count);
        Assert.False(preview.Value.AlreadyJoined);
    }

    [Fact]
    public async Task PreviewGameAsync_trims_and_uppercases_the_code()
    {
        var h = await CreateAsync(nameof(PreviewGameAsync_trims_and_uppercases_the_code));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        var preview = await h.Games.PreviewGameAsync($"  {created.Value!.JoinCode.ToLowerInvariant()}  ", StrangerId);

        Assert.True(preview.IsOk);
    }

    [Fact]
    public async Task PreviewGameAsync_reports_a_caller_who_is_already_in_the_game()
    {
        var h = await CreateAsync(nameof(PreviewGameAsync_reports_a_caller_who_is_already_in_the_game));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);

        var preview = await h.Games.PreviewGameAsync(created.Value!.JoinCode, HostId);

        Assert.True(preview.Value!.AlreadyJoined);
    }

    [Fact]
    public async Task PreviewGameAsync_refuses_an_unknown_code()
    {
        var h = await CreateAsync(nameof(PreviewGameAsync_refuses_an_unknown_code));

        var preview = await h.Games.PreviewGameAsync("ZZZZZZ", StrangerId);

        Assert.Equal(GameResultStatus.JoinCodeInvalid, preview.Status);
    }

    [Fact]
    public async Task PreviewGameAsync_refuses_an_empty_code_rather_than_throwing()
    {
        var h = await CreateAsync(nameof(PreviewGameAsync_refuses_an_empty_code_rather_than_throwing));

        // The endpoint coalesces a missing query parameter to "", so this is the real input.
        Assert.Equal(GameResultStatus.JoinCodeInvalid, (await h.Games.PreviewGameAsync("", StrangerId)).Status);
        Assert.Equal(GameResultStatus.JoinCodeInvalid, (await h.Games.PreviewGameAsync("   ", StrangerId)).Status);
    }

    [Fact]
    public async Task PreviewGameAsync_refuses_a_code_for_a_completed_game()
    {
        var h = await CreateAsync(nameof(PreviewGameAsync_refuses_a_code_for_a_completed_game));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);
        var code = created.Value!.JoinCode;

        await h.Games.JoinGameAsync(new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, FriendId);
        await h.Games.StartGameAsync(created.Value.GameId, HostId);
        await h.Games.CompleteGameAsync(created.Value.GameId, HostId);

        // Matches the join lookup exactly: the unique index only covers live games.
        Assert.Equal(GameResultStatus.JoinCodeInvalid,
            (await h.Games.PreviewGameAsync(code, StrangerId)).Status);
    }

    [Fact]
    public async Task PreviewGameAsync_reports_a_nine_hole_games_own_hole_set()
    {
        var h = await CreateAsync(nameof(PreviewGameAsync_reports_a_nine_hole_games_own_hole_set));
        var created = await h.Games.CreateGameAsync(
            CreateRequest(h, teeboxId: h.NineHoleTeeboxId, fullRound: false, frontNine: true), HostId);

        var preview = await h.Games.PreviewGameAsync(created.Value!.JoinCode, StrangerId);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], preview.Value!.HoleNumbers);
    }

    // ── Per-participant holes ──

    [Fact]
    public async Task GetGameAsync_returns_the_holes_a_linked_round_has_scored()
    {
        var h = await CreateAsync(nameof(GetGameAsync_returns_the_holes_a_linked_round_has_scored));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);
        var gameId = created.Value!.GameId;

        await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = created.Value.JoinCode, TeeboxId = h.TeeboxId }, FriendId);

        var round = await StartRoundAsync(h, HostId, h.TeeboxId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);
        await h.Games.StartGameAsync(gameId, HostId);

        await h.Entry.UpsertHoleAsync(round, 1, new UpsertHoleRequest { Score = 5 }, HostId);
        await h.Entry.UpsertHoleAsync(round, 3, new UpsertHoleRequest { Score = 4 }, HostId);

        var state = await h.Games.GetGameAsync(gameId, HostId);
        var host = state.Value!.Participants.Single(p => p.UserId == HostId);

        Assert.Equal(2, host.Holes.Count);
        Assert.Equal([1, 3], host.Holes.Select(x => x.HoleNumber));
        Assert.Equal((short)5, host.Holes[0].Strokes);
        Assert.Equal((short)4, host.Holes[1].Strokes);

        // A player who has entered nothing gets an empty list, not null.
        Assert.Empty(state.Value.Participants.Single(p => p.UserId == FriendId).Holes);
    }

    [Fact]
    public async Task GetGameAsync_returns_the_holes_a_host_entered_for_a_guest()
    {
        var h = await CreateAsync(nameof(GetGameAsync_returns_the_holes_a_host_entered_for_a_guest));
        var created = await h.Games.CreateGameAsync(CreateRequest(h, GameType.Skins), HostId);
        var gameId = created.Value!.GameId;

        var added = await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        var guestId = added.Value!.Participants.Single(p => p.IsGuest).ParticipantId;
        await h.Games.StartGameAsync(gameId, HostId);

        await h.Games.UpsertParticipantHoleAsync(gameId, guestId, 2, new UpsertGameHoleRequest { Strokes = 6 }, HostId);

        var state = await h.Games.GetGameAsync(gameId, HostId);
        var guest = state.Value!.Participants.Single(p => p.ParticipantId == guestId);

        var hole = Assert.Single(guest.Holes);
        Assert.Equal(2, hole.HoleNumber);
        Assert.Equal((short)6, hole.Strokes);
    }

    [Fact]
    public async Task GetGameAsync_holes_survive_a_clear()
    {
        var h = await CreateAsync(nameof(GetGameAsync_holes_survive_a_clear));
        var created = await h.Games.CreateGameAsync(CreateRequest(h, GameType.Skins), HostId);
        var gameId = created.Value!.GameId;

        var added = await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        var guestId = added.Value!.Participants.Single(p => p.IsGuest).ParticipantId;
        await h.Games.StartGameAsync(gameId, HostId);

        await h.Games.UpsertParticipantHoleAsync(gameId, guestId, 2, new UpsertGameHoleRequest { Strokes = 6 }, HostId);
        var cleared = await h.Games.ClearParticipantHoleAsync(gameId, guestId, 2, HostId);

        Assert.Empty(cleared.Value!.Participants.Single(p => p.ParticipantId == guestId).Holes);
    }

    [Fact]
    public async Task GetGameAsync_holes_agree_with_the_holes_entered_count()
    {
        var h = await CreateAsync(nameof(GetGameAsync_holes_agree_with_the_holes_entered_count));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);
        var gameId = created.Value!.GameId;

        await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = created.Value.JoinCode, TeeboxId = h.TeeboxId }, FriendId);

        var round = await StartRoundAsync(h, HostId, h.TeeboxId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);
        await h.Games.StartGameAsync(gameId, HostId);

        foreach (var n in new[] { 1, 2, 5 })
        {
            await h.Entry.UpsertHoleAsync(round, n, new UpsertHoleRequest { Score = (short)Pars[n - 1] }, HostId);
        }

        var state = await h.Games.GetGameAsync(gameId, HostId);

        // The two fields are derived separately; a client that trusts one over the other must not
        // be able to tell the difference.
        foreach (var participant in state.Value!.Participants)
        {
            Assert.Equal(participant.HolesEntered, participant.Holes.Count);
        }
    }

    [Fact]
    public async Task GetGameAsync_holes_are_confined_to_the_games_own_hole_set()
    {
        var h = await CreateAsync(nameof(GetGameAsync_holes_are_confined_to_the_games_own_hole_set));

        // A front-nine game, but the linked round is a full eighteen on the same course.
        var created = await h.Games.CreateGameAsync(
            CreateRequest(h, fullRound: false, frontNine: true), HostId);
        var gameId = created.Value!.GameId;

        await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = created.Value.JoinCode, TeeboxId = h.TeeboxId }, FriendId);

        var round = await StartRoundAsync(h, HostId, h.TeeboxId);
        await h.Games.LinkRoundAsync(gameId, round, HostId);
        await h.Games.StartGameAsync(gameId, HostId);

        await h.Entry.UpsertHoleAsync(round, 3, new UpsertHoleRequest { Score = 4 }, HostId);
        await h.Entry.UpsertHoleAsync(round, 14, new UpsertHoleRequest { Score = 7 }, HostId);

        var state = await h.Games.GetGameAsync(gameId, HostId);
        var host = state.Value!.Participants.Single(p => p.UserId == HostId);

        // Hole 14 is in the round but not in the game — it must not leak into the game's card.
        var hole = Assert.Single(host.Holes);
        Assert.Equal(3, hole.HoleNumber);
    }

    [Fact]
    public async Task GetGameAsync_holes_are_null_free_and_ordered()
    {
        var h = await CreateAsync(nameof(GetGameAsync_holes_are_null_free_and_ordered));
        var created = await h.Games.CreateGameAsync(CreateRequest(h, GameType.Skins), HostId);
        var gameId = created.Value!.GameId;

        var added = await h.Games.AddParticipantAsync(gameId, new AddParticipantRequest
        {
            DisplayName = "Wally",
            TeeboxId = h.TeeboxId
        }, HostId);

        var guestId = added.Value!.Participants.Single(p => p.IsGuest).ParticipantId;
        await h.Games.StartGameAsync(gameId, HostId);

        // Entered out of order on purpose.
        foreach (var n in new[] { 7, 2, 5 })
        {
            await h.Games.UpsertParticipantHoleAsync(
                gameId, guestId, n, new UpsertGameHoleRequest { Strokes = 5 }, HostId);
        }

        var state = await h.Games.GetGameAsync(gameId, HostId);
        var guest = state.Value!.Participants.Single(p => p.ParticipantId == guestId);

        Assert.Equal([2, 5, 7], guest.Holes.Select(x => x.HoleNumber));
    }

    // ── What the preview advertises as joinable ──

    [Fact]
    public async Task JoinGameAsync_refuses_a_third_side_into_a_running_match()
    {
        var h = await CreateAsync(nameof(JoinGameAsync_refuses_a_third_side_into_a_running_match));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);
        var gameId = created.Value!.GameId;
        var code = created.Value.JoinCode;

        await h.Games.JoinGameAsync(new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, FriendId);
        await h.Games.StartGameAsync(gameId, HostId);

        // The field was validated once at start. Joining is the only path that can change it
        // afterwards, so it has to re-run that check — a third side would otherwise leave the
        // match unscoreable mid-round.
        var joined = await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, StrangerId);

        Assert.Equal(GameResultStatus.ParticipantCountInvalid, joined.Status);
        Assert.Contains("two sides", joined.Detail);

        // And the running match is untouched.
        var state = await h.Games.GetGameAsync(gameId, HostId);
        Assert.Equal(2, state.Value!.Participants.Count);
        Assert.Equal(2, ((MatchPlayScoreboard)state.Value.Scoreboard!).Standings.Count);
    }

    [Fact]
    public async Task JoinGameAsync_still_allows_a_late_arrival_into_a_running_skins_game()
    {
        var h = await CreateAsync(nameof(JoinGameAsync_still_allows_a_late_arrival_into_a_running_skins_game));
        var created = await h.Games.CreateGameAsync(CreateRequest(h, GameType.Skins), HostId);
        var gameId = created.Value!.GameId;
        var code = created.Value.JoinCode;

        await h.Games.JoinGameAsync(new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, FriendId);
        await h.Games.StartGameAsync(gameId, HostId);

        // Skins takes any number of individuals, so a friend arriving on the 2nd tee is fine.
        var joined = await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, StrangerId);

        Assert.True(joined.IsOk);
        Assert.Equal(3, joined.Value!.Participants.Count);
        Assert.IsType<SkinsScoreboard>(joined.Value.Scoreboard);
    }

    [Fact]
    public async Task JoinGameAsync_refuses_a_teamed_arrival_into_a_running_skins_game()
    {
        var h = await CreateAsync(nameof(JoinGameAsync_refuses_a_teamed_arrival_into_a_running_skins_game));
        var created = await h.Games.CreateGameAsync(CreateRequest(h, GameType.Skins), HostId);
        var gameId = created.Value!.GameId;
        var code = created.Value.JoinCode;

        await h.Games.JoinGameAsync(new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, FriendId);
        await h.Games.StartGameAsync(gameId, HostId);

        // The joiner's own team value is part of the field being validated.
        var joined = await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId, Team = 1 }, StrangerId);

        Assert.Equal(GameResultStatus.ParticipantCountInvalid, joined.Status);
    }

    [Fact]
    public async Task JoinGameAsync_leaves_a_game_in_setup_alone()
    {
        var h = await CreateAsync(nameof(JoinGameAsync_leaves_a_game_in_setup_alone));
        var created = await h.Games.CreateGameAsync(CreateRequest(h), HostId);
        var code = created.Value!.JoinCode;

        await h.Games.JoinGameAsync(new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, FriendId);

        // A host still builds the field in any order before starting — three people may sit in
        // a match-play game until StartGameAsync refuses it.
        var third = await h.Games.JoinGameAsync(
            new JoinGameRequest { JoinCode = code, TeeboxId = h.TeeboxId }, StrangerId);

        Assert.True(third.IsOk);
        Assert.Equal(3, third.Value!.Participants.Count);
        Assert.Equal(GameResultStatus.ParticipantCountInvalid,
            (await h.Games.StartGameAsync(created.Value.GameId, HostId)).Status);
    }
}
