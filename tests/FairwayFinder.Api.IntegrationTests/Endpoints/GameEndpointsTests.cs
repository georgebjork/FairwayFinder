using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.IntegrationTests.Common;
using static FairwayFinder.Api.IntegrationTests.Infrastructure.RoundFlows;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

/// <summary>
/// Head-to-head games end to end: create, preview and join by code, start, score, post — plus
/// the host/participant permission rules and the ETag'd poll.
/// </summary>
public class GameEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CreateGameRequest NewGame(SeededCourse course, GameType type = GameType.MatchPlay, bool frontNine = true) => new()
    {
        GameType = type,
        CourseId = course.CourseId,
        TeeboxId = course.TeeboxId,
        DatePlayed = DefaultDate,
        FullRound = !frontNine,
        FrontNine = frontNine,
    };

    private static async Task<GameStateResponse> CreateGameAsync(ApiUser host, CreateGameRequest request) =>
        await ReadAsync<GameStateResponse>(await host.Client.PostAsJsonAsync("/api/games", request, Json), HttpStatusCode.Created);

    private static async Task<GameStateResponse> JoinAsync(ApiUser player, GameStateResponse game, SeededCourse course) =>
        await ReadAsync<GameStateResponse>(await player.Client.PostAsJsonAsync("/api/games/join",
            new JoinGameRequest { JoinCode = game.JoinCode, TeeboxId = course.TeeboxId }, Json));

    private static async Task<GameStateResponse> ScoreAsync(ApiUser caller, long gameId, long participantId, int hole, int strokes) =>
        await ReadAsync<GameStateResponse>(await caller.Client.PutAsJsonAsync(
            $"/api/games/{gameId}/participants/{participantId}/holes/{hole}",
            new UpsertGameHoleRequest { Strokes = (short)strokes }, Json));

    private static long ParticipantIdOf(GameStateResponse game, string userId) =>
        game.Participants.Single(p => p.UserId == userId).ParticipantId;

    [Fact]
    public async Task Match_play_from_create_to_posted_result()
    {
        var alice = await SignInNewUserAsync(firstName: "Alice");
        var bob = await SignInNewUserAsync(firstName: "Bob");
        var course = await Data.CreateCourseAsync();

        var game = await CreateGameAsync(alice, NewGame(course));
        Assert.Equal(GameState.Setup, game.State);
        Assert.Equal(alice.Id, game.HostUserId);
        Assert.False(string.IsNullOrWhiteSpace(game.JoinCode));
        Assert.Equal(Enumerable.Range(1, 9), game.HoleNumbers);

        var preview = await GetAsync<GameJoinPreviewResponse>(bob.Client, $"/api/games/preview?joinCode={game.JoinCode}");
        Assert.Equal(game.GameId, preview.GameId);
        Assert.Equal(1, preview.ParticipantCount);
        Assert.False(preview.AlreadyJoined);

        game = await JoinAsync(bob, game, course);
        Assert.Equal(2, game.Participants.Count);

        game = await ReadAsync<GameStateResponse>(await alice.Client.PostAsync($"/api/games/{game.GameId}/start", null));
        Assert.Equal(GameState.Active, game.State);

        var aliceId = ParticipantIdOf(game, alice.Id);
        var bobId = ParticipantIdOf(game, bob.Id);

        // Alice wins hole 1; every other hole is halved. Each player enters their own line.
        foreach (var hole in game.HoleNumbers)
        {
            var par = course.Pars[hole - 1];
            await ScoreAsync(alice, game.GameId, aliceId, hole, hole == 1 ? par - 1 : par);
            game = await ScoreAsync(bob, game.GameId, bobId, hole, par);
        }

        Assert.NotNull(game.Scoreboard);
        Assert.Equal(9, game.Scoreboard.HolesPlayed);
        Assert.True(game.Scoreboard.Standings.Single(s => s.ParticipantId == aliceId).IsLeader);

        var posted = await ReadAsync<GameStateResponse>(await alice.Client.PostAsync($"/api/games/{game.GameId}/complete", null));
        Assert.Equal(GameState.Completed, posted.State);

        // Posted games leave the active list but stay in history.
        Assert.Empty(await GetAsync<List<GameSummaryResponse>>(bob.Client, "/api/games"));
        Assert.Single(await GetAsync<List<GameSummaryResponse>>(bob.Client, "/api/games?activeOnly=false"));
    }

    [Fact]
    public async Task A_game_cannot_start_with_one_player_and_explains_why()
    {
        var alice = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var game = await CreateGameAsync(alice, NewGame(course));

        var response = await alice.Client.PostAsync($"/api/games/{game.GameId}/start", null);

        var problem = await ReadAsync<JsonElement>(response, HttpStatusCode.Conflict);
        Assert.Contains("at least two players", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Only_the_host_runs_the_game_and_outsiders_cannot_see_it()
    {
        var host = await SignInNewUserAsync();
        var player = await SignInNewUserAsync();
        var outsider = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var game = await CreateGameAsync(host, NewGame(course));
        game = await JoinAsync(player, game, course);

        await AssertStatusAsync(HttpStatusCode.Forbidden, await player.Client.PostAsync($"/api/games/{game.GameId}/start", null));
        await AssertStatusAsync(HttpStatusCode.NotFound, await outsider.Client.GetAsync($"/api/games/{game.GameId}"));

        await AssertStatusAsync(HttpStatusCode.OK, await host.Client.PostAsync($"/api/games/{game.GameId}/start", null));

        // A player may not write someone else's line.
        var response = await player.Client.PutAsJsonAsync(
            $"/api/games/{game.GameId}/participants/{ParticipantIdOf(game, host.Id)}/holes/1",
            new UpsertGameHoleRequest { Strokes = 9 });
        await AssertStatusAsync(HttpStatusCode.Forbidden, response);
    }

    [Fact]
    public async Task Joining_twice_or_with_a_bad_code_is_refused()
    {
        var host = await SignInNewUserAsync();
        var player = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var game = await CreateGameAsync(host, NewGame(course));
        await JoinAsync(player, game, course);

        var again = await player.Client.PostAsJsonAsync("/api/games/join",
            new JoinGameRequest { JoinCode = game.JoinCode, TeeboxId = course.TeeboxId });
        var bogus = await player.Client.PostAsJsonAsync("/api/games/join",
            new JoinGameRequest { JoinCode = "NOPE99", TeeboxId = course.TeeboxId });

        await AssertStatusAsync(HttpStatusCode.Conflict, again);
        await AssertStatusAsync(HttpStatusCode.NotFound, bogus);
        await AssertStatusAsync(HttpStatusCode.NotFound, await player.Client.GetAsync("/api/games/preview?joinCode=NOPE99"));
    }

    [Fact]
    public async Task Skins_with_a_guest_and_a_friend_added_by_the_host()
    {
        var host = await SignInNewUserAsync(firstName: "Host");
        var friend = await SignInNewUserAsync(firstName: "Friend");
        var stranger = await SignInNewUserAsync(firstName: "Stranger");
        await MakeFriendsAsync(host, friend);
        var course = await Data.CreateCourseAsync();
        var game = await CreateGameAsync(host, NewGame(course, GameType.Skins));

        game = await ReadAsync<GameStateResponse>(await host.Client.PostAsJsonAsync($"/api/games/{game.GameId}/participants",
            new AddParticipantRequest { DisplayName = "Guest Gary", TeeboxId = course.TeeboxId }));
        game = await ReadAsync<GameStateResponse>(await host.Client.PostAsJsonAsync($"/api/games/{game.GameId}/participants",
            new AddParticipantRequest { UserId = friend.Id, TeeboxId = course.TeeboxId }));

        // Only friends can be added by user id.
        var notFriend = await host.Client.PostAsJsonAsync($"/api/games/{game.GameId}/participants",
            new AddParticipantRequest { UserId = stranger.Id, TeeboxId = course.TeeboxId });
        await AssertStatusAsync(HttpStatusCode.Conflict, notFriend);

        Assert.Equal(3, game.Participants.Count);
        var guest = game.Participants.Single(p => p.IsGuest);

        game = await ReadAsync<GameStateResponse>(await host.Client.PostAsync($"/api/games/{game.GameId}/start", null));

        // The host scores the guest. Guest takes hole 1 outright.
        game = await ScoreAsync(host, game.GameId, guest.ParticipantId, 1, 3);
        await ScoreAsync(host, game.GameId, ParticipantIdOf(game, host.Id), 1, 5);
        game = await ScoreAsync(friend, game.GameId, ParticipantIdOf(game, friend.Id), 1, 5);

        Assert.NotNull(game.Scoreboard);
        Assert.Equal(1, game.Scoreboard.HolesPlayed);
        Assert.True(game.Scoreboard.Standings.Single(s => s.ParticipantId == guest.ParticipantId).IsLeader);

        // Clearing the hole takes it back off the board.
        game = await ReadAsync<GameStateResponse>(await host.Client.DeleteAsync(
            $"/api/games/{game.GameId}/participants/{guest.ParticipantId}/holes/1"));
        Assert.Equal(0, game.Participants.Single(p => p.IsGuest).HolesEntered);
    }

    [Fact]
    public async Task Host_can_edit_and_remove_participants_during_setup()
    {
        var host = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var game = await CreateGameAsync(host, NewGame(course, GameType.Skins));
        game = await ReadAsync<GameStateResponse>(await host.Client.PostAsJsonAsync($"/api/games/{game.GameId}/participants",
            new AddParticipantRequest { DisplayName = "Guest", TeeboxId = course.TeeboxId }));
        var guestId = game.Participants.Single(p => p.IsGuest).ParticipantId;

        game = await ReadAsync<GameStateResponse>(await host.Client.PutAsJsonAsync(
            $"/api/games/{game.GameId}/participants/{guestId}",
            new UpdateParticipantRequest { CourseHandicap = 4, DisplayName = "Renamed Guest" }));
        var edited = game.Participants.Single(p => p.ParticipantId == guestId);
        Assert.Equal(4, edited.CourseHandicap);
        Assert.Equal("Renamed Guest", edited.DisplayName);

        game = await ReadAsync<GameStateResponse>(await host.Client.DeleteAsync($"/api/games/{game.GameId}/participants/{guestId}"));
        Assert.Single(game.Participants);
    }

    [Fact]
    public async Task Poll_answers_not_modified_until_the_scoreboard_moves()
    {
        var host = await SignInNewUserAsync();
        var player = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var game = await CreateGameAsync(host, NewGame(course));
        game = await JoinAsync(player, game, course);

        var first = await host.Client.GetAsync($"/api/games/{game.GameId}");
        await AssertStatusAsync(HttpStatusCode.OK, first);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);

        using var unchanged = new HttpRequestMessage(HttpMethod.Get, $"/api/games/{game.GameId}");
        unchanged.Headers.IfNoneMatch.Add(etag);
        Assert.Equal(HttpStatusCode.NotModified, (await host.Client.SendAsync(unchanged)).StatusCode);

        await host.Client.PostAsync($"/api/games/{game.GameId}/start", null);

        using var changed = new HttpRequestMessage(HttpMethod.Get, $"/api/games/{game.GameId}");
        changed.Headers.IfNoneMatch.Add(etag);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(changed)).StatusCode);
    }

    [Fact]
    public async Task Abandoned_games_accept_no_more_scores()
    {
        var host = await SignInNewUserAsync();
        var player = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var game = await CreateGameAsync(host, NewGame(course));
        game = await JoinAsync(player, game, course);
        await host.Client.PostAsync($"/api/games/{game.GameId}/start", null);

        var abandoned = await ReadAsync<GameStateResponse>(await host.Client.PostAsync($"/api/games/{game.GameId}/abandon", null));
        Assert.Equal(GameState.Abandoned, abandoned.State);

        var write = await host.Client.PutAsJsonAsync(
            $"/api/games/{game.GameId}/participants/{ParticipantIdOf(game, host.Id)}/holes/1",
            new UpsertGameHoleRequest { Strokes = 4 });
        await AssertStatusAsync(HttpStatusCode.Conflict, write);
        await AssertStatusAsync(HttpStatusCode.Conflict, await host.Client.PostAsync($"/api/games/{game.GameId}/complete", null));
    }

    [Fact]
    public async Task A_linked_round_is_the_source_of_that_players_strokes()
    {
        var host = await SignInNewUserAsync();
        var player = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var round = await StartRoundAsync(host.Client, StartRequest(course, fullRound: false, frontNine: true));

        var request = NewGame(course);
        request.RoundId = round.RoundId;
        var game = await CreateGameAsync(host, request);
        game = await JoinAsync(player, game, course);
        await host.Client.PostAsync($"/api/games/{game.GameId}/start", null);
        var hostParticipant = ParticipantIdOf(game, host.Id);

        // Scores entered on the round flow into the game...
        await UpsertHoleAsync(host.Client, round.RoundId, 1, 4);
        game = await GetAsync<GameStateResponse>(host.Client, $"/api/games/{game.GameId}");
        Assert.Equal(1, game.Participants.Single(p => p.ParticipantId == hostParticipant).HolesEntered);
        Assert.Equal(round.RoundId, game.Participants.Single(p => p.ParticipantId == hostParticipant).RoundId);

        // ...and writing them on the game instead is refused rather than silently ignored.
        var direct = await host.Client.PutAsJsonAsync(
            $"/api/games/{game.GameId}/participants/{hostParticipant}/holes/2",
            new UpsertGameHoleRequest { Strokes = 4 });
        await AssertStatusAsync(HttpStatusCode.Conflict, direct);

        // The round's own read shows the game it belongs to.
        var roundView = await GetAsync<RoundResponse>(host.Client, $"/api/rounds/{round.RoundId}");
        Assert.Equal(game.GameId, Assert.Single(roundView.Games).GameId);
    }

    [Fact]
    public async Task Create_game_validation_rejects_a_bad_handicap_allowance()
    {
        var host = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var request = NewGame(course);
        request.HandicapAllowancePercent = 0;

        var response = await host.Client.PostAsJsonAsync("/api/games", request, Json);

        var problem = await ReadAsync<JsonElement>(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty("HandicapAllowancePercent", out _));
    }
}
