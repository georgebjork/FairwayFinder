using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// The two directions a game and a round see each other: a round showing the games it fed, and a
/// game telling a golfer when their own round is ready to post.
///
/// Completing a game deliberately does not post anybody's round — that would fire another
/// golfer's stats, strokes gained, and friend notifications off the host's button.
/// </summary>
public class GameRoundLinkageTests
{
    private const string HostId = "host-user";
    private const string FriendId = "friend-user";

    private static readonly int[] Pars = [4, 5, 3, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 4, 5, 4];

    private sealed record Harness(
        GameService Games,
        RoundService Rounds,
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

        var course = new Course { CourseName = "Linkage Links", CreatedBy = HostId, UpdatedBy = HostId };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var blue = NewTeebox(course.CourseId, "Blue", 18);
        var nine = NewTeebox(course.CourseId, "Blue Nine", 9, isNineHole: true);
        db.Teeboxes.AddRange(blue, nine);
        await db.SaveChangesAsync();

        AddHoles(db, blue, 18);
        AddHoles(db, nine, 9);

        db.Users.AddRange(NewUser(HostId, "Dale", "Host"), NewUser(FriendId, "Sam", "Friend"));
        await db.SaveChangesAsync();

        return new Harness(games, rounds, entry, factory, course.CourseId, blue.TeeboxId, nine.TeeboxId);
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

    private static async Task<long> StartRoundAsync(Harness h, string userId, long? teeboxId = null)
    {
        var started = await h.Entry.StartRoundAsync(new StartRoundRequest
        {
            CourseId = h.CourseId,
            TeeboxId = teeboxId ?? h.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = true
        }, userId);

        return started.Value!.RoundId;
    }

    private static async Task PlayAsync(Harness h, long roundId, string userId, IEnumerable<int> holes, int offset = 0)
    {
        foreach (var n in holes)
        {
            await h.Entry.UpsertHoleAsync(roundId, n,
                new UpsertHoleRequest { Score = (short)(Pars[n - 1] + offset) }, userId);
        }
    }

    /// <summary>A started match between the host and their friend, each on their own round.</summary>
    private static async Task<(long GameId, long HostRound, long FriendRound)> StartedMatchAsync(
        Harness h, GameType gameType = GameType.MatchPlay, long? teeboxId = null,
        bool fullRound = true, bool frontNine = false)
    {
        var created = await h.Games.CreateGameAsync(new CreateGameRequest
        {
            GameType = gameType,
            CourseId = h.CourseId,
            TeeboxId = teeboxId ?? h.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = fullRound,
            FrontNine = frontNine
        }, HostId);

        var gameId = created.Value!.GameId;

        await h.Games.JoinGameAsync(new JoinGameRequest
        {
            JoinCode = created.Value.JoinCode,
            TeeboxId = teeboxId ?? h.TeeboxId
        }, FriendId);

        var hostRound = await StartRoundAsync(h, HostId, teeboxId);
        var friendRound = await StartRoundAsync(h, FriendId, teeboxId);

        await h.Games.LinkRoundAsync(gameId, hostRound, HostId);
        await h.Games.LinkRoundAsync(gameId, friendRound, FriendId);
        await h.Games.StartGameAsync(gameId, HostId);

        return (gameId, hostRound, friendRound);
    }

    // ── Round -> games ──

    [Fact]
    public async Task GetRoundByIdAsync_lists_the_games_the_round_was_played_for()
    {
        var h = await CreateAsync(nameof(GetRoundByIdAsync_lists_the_games_the_round_was_played_for));
        var (gameId, hostRound, _) = await StartedMatchAsync(h);

        await PlayAsync(h, hostRound, HostId, Enumerable.Range(1, 18));

        var round = await h.Rounds.GetRoundByIdAsync(hostRound, BaselineLevel.Scratch);

        var game = Assert.Single(round!.Games);
        Assert.Equal(gameId, game.GameId);
        Assert.Equal(GameType.MatchPlay, game.GameType);
        Assert.Equal(GameState.Active, game.State);
        Assert.Equal("Linkage Links", game.CourseName);
        Assert.Equal(2, game.ParticipantCount);

        // Live games have no stored snapshot, so no result line yet.
        Assert.Null(game.ResultSummary);
    }

    [Fact]
    public async Task GetRoundByIdAsync_shows_how_a_posted_game_finished()
    {
        var h = await CreateAsync(nameof(GetRoundByIdAsync_shows_how_a_posted_game_finished));
        var (gameId, hostRound, friendRound) = await StartedMatchAsync(h);

        // Host wins the first four, the rest are halved: 4 & 3 by hole 15.
        await PlayAsync(h, hostRound, HostId, Enumerable.Range(1, 18));
        await PlayAsync(h, friendRound, FriendId, Enumerable.Range(1, 4), offset: 1);
        await PlayAsync(h, friendRound, FriendId, Enumerable.Range(5, 14));

        await h.Games.CompleteGameAsync(gameId, HostId);

        var round = await h.Rounds.GetRoundByIdAsync(hostRound, BaselineLevel.Scratch);
        var game = Assert.Single(round!.Games);

        Assert.Equal(GameState.Completed, game.State);
        Assert.Contains("4 & 3", game.ResultSummary);
    }

    [Fact]
    public async Task GetRoundByIdAsync_lists_every_game_one_round_fed()
    {
        var h = await CreateAsync(nameof(GetRoundByIdAsync_lists_every_game_one_round_fed));
        var (matchId, hostRound, _) = await StartedMatchAsync(h);

        // The same round also carries a skins game — explicitly supported, and the reason there
        // is no unique constraint on round_id.
        var skins = await h.Games.CreateGameAsync(new CreateGameRequest
        {
            GameType = GameType.Skins,
            CourseId = h.CourseId,
            TeeboxId = h.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
            FullRound = true,
            RoundId = hostRound
        }, HostId);

        var round = await h.Rounds.GetRoundByIdAsync(hostRound, BaselineLevel.Scratch);

        Assert.Equal(2, round!.Games.Count);
        Assert.Contains(round.Games, g => g.GameId == matchId && g.GameType == GameType.MatchPlay);
        Assert.Contains(round.Games, g => g.GameId == skins.Value!.GameId && g.GameType == GameType.Skins);
    }

    [Fact]
    public async Task GetRoundByIdAsync_returns_no_games_for_a_round_played_alone()
    {
        var h = await CreateAsync(nameof(GetRoundByIdAsync_returns_no_games_for_a_round_played_alone));
        var roundId = await StartRoundAsync(h, HostId);

        var round = await h.Rounds.GetRoundByIdAsync(roundId, BaselineLevel.Scratch);

        Assert.Empty(round!.Games);
    }

    [Fact]
    public async Task GetRoundByIdAsync_drops_a_game_that_has_been_deleted()
    {
        var h = await CreateAsync(nameof(GetRoundByIdAsync_drops_a_game_that_has_been_deleted));
        var (gameId, hostRound, _) = await StartedMatchAsync(h);

        await using (var db = h.Factory.CreateDbContext())
        {
            var game = db.Games.Single(g => g.GameId == gameId);
            game.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        var round = await h.Rounds.GetRoundByIdAsync(hostRound, BaselineLevel.Scratch);

        Assert.Empty(round!.Games);
    }

    // ── Game -> "your round is ready to post" ──

    [Fact]
    public async Task GetGameAsync_flags_a_round_as_ready_once_its_card_is_full()
    {
        var h = await CreateAsync(nameof(GetGameAsync_flags_a_round_as_ready_once_its_card_is_full));
        var (gameId, hostRound, _) = await StartedMatchAsync(h);

        var partway = await h.Games.GetGameAsync(gameId, HostId);
        var host = partway.Value!.Participants.Single(p => p.UserId == HostId);
        Assert.False(host.RoundReadyToPost);
        Assert.False(host.RoundIsComplete);

        await PlayAsync(h, hostRound, HostId, Enumerable.Range(1, 18));

        var full = await h.Games.GetGameAsync(gameId, HostId);
        host = full.Value!.Participants.Single(p => p.UserId == HostId);

        Assert.True(host.RoundReadyToPost);
        Assert.False(host.RoundIsComplete);

        // The friend has played nothing, so theirs is not ready.
        Assert.False(full.Value.Participants.Single(p => p.UserId == FriendId).RoundReadyToPost);
    }

    [Fact]
    public async Task GetGameAsync_does_not_flag_a_conceded_match_as_ready()
    {
        var h = await CreateAsync(nameof(GetGameAsync_does_not_flag_a_conceded_match_as_ready));
        var (gameId, hostRound, friendRound) = await StartedMatchAsync(h);

        // Won 4 & 3 on the 15th; nobody plays 16-18.
        await PlayAsync(h, hostRound, HostId, Enumerable.Range(1, 15));
        await PlayAsync(h, friendRound, FriendId, Enumerable.Range(1, 4), offset: 1);
        await PlayAsync(h, friendRound, FriendId, Enumerable.Range(5, 11));

        var state = await h.Games.GetGameAsync(gameId, HostId);
        var board = Assert.IsType<MatchPlayScoreboard>(state.Value!.Scoreboard);
        Assert.Equal("4 & 3", board.ResultLine);

        // A conceded match leaves an unpostable card — there is no valid eighteen to post.
        Assert.All(state.Value.Participants, p => Assert.False(p.RoundReadyToPost));
    }

    [Fact]
    public async Task CompleteGameAsync_leaves_every_round_open()
    {
        var h = await CreateAsync(nameof(CompleteGameAsync_leaves_every_round_open));
        var (gameId, hostRound, friendRound) = await StartedMatchAsync(h);

        await PlayAsync(h, hostRound, HostId, Enumerable.Range(1, 18));
        await PlayAsync(h, friendRound, FriendId, Enumerable.Range(1, 18));

        var completed = await h.Games.CompleteGameAsync(gameId, HostId);

        // Posting is the golfer's own call: doing it here would fire the friend's stats, strokes
        // gained, and friend notifications off the host pressing a button.
        Assert.All(completed.Value!.Participants, p =>
        {
            Assert.False(p.RoundIsComplete);
            Assert.True(p.RoundReadyToPost);
        });

        await using var db = h.Factory.CreateDbContext();
        Assert.All(db.Rounds.Where(r => r.RoundId == hostRound || r.RoundId == friendRound),
            r => Assert.False(r.IsComplete));
    }

    [Fact]
    public async Task GetGameAsync_reports_a_round_that_has_since_been_posted()
    {
        var h = await CreateAsync(nameof(GetGameAsync_reports_a_round_that_has_since_been_posted));
        var (gameId, hostRound, _) = await StartedMatchAsync(h);

        await PlayAsync(h, hostRound, HostId, Enumerable.Range(1, 18));
        var posted = await h.Entry.CompleteRoundAsync(hostRound, HostId, BaselineLevel.Scratch);
        Assert.True(posted.IsOk);

        var state = await h.Games.GetGameAsync(gameId, HostId);
        var host = state.Value!.Participants.Single(p => p.UserId == HostId);

        Assert.True(host.RoundIsComplete);
        Assert.False(host.RoundReadyToPost);

        // And the game keeps scoring off it — a game outlives its rounds' completion.
        Assert.Equal(18, host.Holes.Count);
    }

    [Fact]
    public async Task GetGameAsync_never_flags_a_guest_as_having_a_round()
    {
        var h = await CreateAsync(nameof(GetGameAsync_never_flags_a_guest_as_having_a_round));

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

        var guest = added.Value!.Participants.Single(p => p.IsGuest);

        Assert.False(guest.RoundIsComplete);
        Assert.False(guest.RoundReadyToPost);
    }

    [Fact]
    public async Task GetGameAsync_judges_readiness_on_the_rounds_own_holes_not_the_games()
    {
        var h = await CreateAsync(nameof(GetGameAsync_judges_readiness_on_the_rounds_own_holes_not_the_games));

        // A front-nine game, but the rounds behind it are full eighteens.
        var (gameId, hostRound, _) = await StartedMatchAsync(h, fullRound: false, frontNine: true);

        await PlayAsync(h, hostRound, HostId, Enumerable.Range(1, 9));

        var afterNine = await h.Games.GetGameAsync(gameId, HostId);
        Assert.True(afterNine.Value!.Participants.Single(p => p.UserId == HostId).RoundReadyToPost);

        // Playing on past the game's hole set makes the round an incomplete eighteen again.
        await PlayAsync(h, hostRound, HostId, Enumerable.Range(10, 4));

        var afterThirteen = await h.Games.GetGameAsync(gameId, HostId);
        Assert.False(afterThirteen.Value!.Participants.Single(p => p.UserId == HostId).RoundReadyToPost);
    }

    [Fact]
    public async Task GetGameAsync_flags_a_nine_hole_round_as_ready_after_nine()
    {
        var h = await CreateAsync(nameof(GetGameAsync_flags_a_nine_hole_round_as_ready_after_nine));
        var (gameId, hostRound, _) = await StartedMatchAsync(
            h, teeboxId: h.NineHoleTeeboxId, fullRound: false, frontNine: true);

        await PlayAsync(h, hostRound, HostId, Enumerable.Range(1, 9));

        var state = await h.Games.GetGameAsync(gameId, HostId);

        Assert.True(state.Value!.Participants.Single(p => p.UserId == HostId).RoundReadyToPost);
    }
}
