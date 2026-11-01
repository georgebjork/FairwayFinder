using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using Microsoft.EntityFrameworkCore;
using static FairwayFinder.Api.IntegrationTests.Infrastructure.RoundFlows;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

/// <summary>
/// Hole-by-hole round entry through the HTTP surface, against real Postgres — including the
/// filtered unique indexes that only exist there (<c>ix_round_user_id_active</c>,
/// <c>ix_score_round_id_hole_id</c>).
/// </summary>
public class RoundEntryTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Full_entry_flow_start_write_clear_complete()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();

        var started = await StartRoundAsync(golfer.Client, StartRequest(course));
        Assert.Equal(18, started.Holes.Count);
        Assert.Equal(course.TeeboxId, started.TeeboxId);

        // Resume sees the open round.
        var active = await GetJsonAsync(golfer.Client, "/api/rounds/active");
        Assert.Equal(started.RoundId, active.GetProperty("roundId").GetInt64());

        var progress = await UpsertHoleAsync(golfer.Client, started.RoundId, 1, 5);
        Assert.Equal(5, progress.Total);
        Assert.Equal(1, progress.HolesEntered);
        Assert.Equal(1, progress.ToPar);

        // Overwriting the same hole updates the existing score rather than adding another.
        progress = await UpsertHoleAsync(golfer.Client, started.RoundId, 1, 4);
        Assert.Equal(4, progress.Total);
        Assert.Equal(1, progress.HolesEntered);

        // Clearing a hole takes it back out of the running total.
        var cleared = await ReadAsync<RoundProgressResponse>(
            await golfer.Client.DeleteAsync($"/api/rounds/{started.RoundId}/holes/1"));
        Assert.Null(cleared.Score);
        Assert.Equal(0, cleared.HolesEntered);

        foreach (var hole in started.Holes)
            await UpsertHoleAsync(golfer.Client, started.RoundId, hole.HoleNumber, (short)hole.Par);

        var posted = await ReadAsync<JsonElement>(
            await golfer.Client.PostAsync($"/api/rounds/{started.RoundId}/complete", null));
        Assert.True(posted.GetProperty("isComplete").GetBoolean());
        Assert.Equal(course.Par, posted.GetProperty("score").GetInt32());

        await AssertStatusAsync(HttpStatusCode.NoContent, await golfer.Client.GetAsync("/api/rounds/active"));

        await using var db = Db();
        var round = await db.Rounds.SingleAsync(r => r.RoundId == started.RoundId);
        Assert.True(round.IsComplete);
        Assert.Equal(18, await db.Scores.CountAsync(s => s.RoundId == round.RoundId && !s.IsDeleted));
    }

    [Fact]
    public async Task Active_round_is_no_content_when_nothing_is_open()
    {
        var golfer = await SignInNewUserAsync();
        await AssertStatusAsync(HttpStatusCode.NoContent, await golfer.Client.GetAsync("/api/rounds/active"));
    }

    [Fact]
    public async Task Starting_a_second_round_while_one_is_open_conflicts_with_the_open_round_id()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var first = await StartRoundAsync(golfer.Client, StartRequest(course));

        var response = await golfer.Client.PostAsJsonAsync("/api/rounds/start", StartRequest(course));

        var body = await ReadAsync<JsonElement>(response, HttpStatusCode.Conflict);
        Assert.Equal("ActiveRoundExists", body.GetProperty("error").GetString());
        Assert.Equal(first.RoundId, body.GetProperty("roundId").GetInt64());
    }

    [Fact]
    public async Task Concurrent_starts_leave_exactly_one_open_round()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => golfer.Client.PostAsJsonAsync("/api/rounds/start", StartRequest(course))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        await using var db = Db();
        Assert.Equal(1, await db.Rounds.CountAsync(r => r.UserId == golfer.Id && !r.IsComplete && !r.IsDeleted));
    }

    [Fact]
    public async Task Concurrent_writes_to_the_same_hole_leave_one_score_row()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var started = await StartRoundAsync(golfer.Client, StartRequest(course));

        var responses = await Task.WhenAll(Enumerable.Range(3, 6).Select(score =>
            golfer.Client.PutAsJsonAsync($"/api/rounds/{started.RoundId}/holes/1",
                new UpsertHoleRequest { Score = (short)score })));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        await using var db = Db();
        Assert.Equal(1, await db.Scores.CountAsync(s => s.RoundId == started.RoundId && !s.IsDeleted));
    }

    [Fact]
    public async Task Completing_a_round_with_holes_missing_names_them()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var started = await StartRoundAsync(golfer.Client, StartRequest(course));
        await UpsertHoleAsync(golfer.Client, started.RoundId, 1, 4);

        var response = await golfer.Client.PostAsync($"/api/rounds/{started.RoundId}/complete", null);

        var problem = await ReadAsync<JsonElement>(response, HttpStatusCode.BadRequest);
        Assert.Contains("Missing a score", problem.GetProperty("errors").GetProperty("Holes")[0].GetString());
    }

    [Fact]
    public async Task Front_nine_round_can_be_posted_after_nine_holes()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var started = await StartRoundAsync(golfer.Client, StartRequest(course, fullRound: false, frontNine: true));

        foreach (var hole in started.Holes.Where(h => h.HoleNumber <= 9))
            await UpsertHoleAsync(golfer.Client, started.RoundId, hole.HoleNumber, (short)hole.Par);

        var posted = await ReadAsync<JsonElement>(
            await golfer.Client.PostAsync($"/api/rounds/{started.RoundId}/complete", null));
        Assert.Equal(course.Pars.Take(9).Sum(), posted.GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task Writing_a_hole_on_a_posted_round_conflicts()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await PlayCompletedRoundAsync(golfer.Client, course);

        var response = await golfer.Client.PutAsJsonAsync($"/api/rounds/{roundId}/holes/1", new UpsertHoleRequest { Score = 3 });

        await AssertStatusAsync(HttpStatusCode.Conflict, response);
    }

    [Fact]
    public async Task Writing_a_hole_the_teebox_does_not_have_is_not_found()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync(nineHole: true);
        var started = await StartRoundAsync(golfer.Client, StartRequest(course, fullRound: false, frontNine: true));

        var response = await golfer.Client.PutAsJsonAsync($"/api/rounds/{started.RoundId}/holes/14", new UpsertHoleRequest { Score = 4 });

        await AssertStatusAsync(HttpStatusCode.NotFound, response);
    }

    [Fact]
    public async Task Another_golfer_cannot_write_to_my_round()
    {
        var owner = await SignInNewUserAsync();
        var intruder = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var started = await StartRoundAsync(owner.Client, StartRequest(course));

        var write = await intruder.Client.PutAsJsonAsync($"/api/rounds/{started.RoundId}/holes/1", new UpsertHoleRequest { Score = 9 });
        var complete = await intruder.Client.PostAsync($"/api/rounds/{started.RoundId}/complete", null);

        await AssertStatusAsync(HttpStatusCode.Forbidden, write);
        await AssertStatusAsync(HttpStatusCode.Forbidden, complete);
    }

    [Fact]
    public async Task Starting_on_an_unknown_teebox_conflicts()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();

        var request = StartRequest(course);
        request.TeeboxId += 10_000;

        var response = await golfer.Client.PostAsJsonAsync("/api/rounds/start", request);

        await AssertStatusAsync(HttpStatusCode.Conflict, response);
    }

    [Fact]
    public async Task Start_request_must_say_which_holes_it_covers()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();

        var response = await golfer.Client.PostAsJsonAsync("/api/rounds/start", StartRequest(course, fullRound: false));

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task An_in_progress_round_stays_out_of_the_round_list_and_stats()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var started = await StartRoundAsync(golfer.Client, StartRequest(course));
        await UpsertHoleAsync(golfer.Client, started.RoundId, 1, 3);

        var rounds = await GetJsonAsync(golfer.Client, "/api/rounds");
        Assert.Equal(0, rounds.GetArrayLength());

        var stats = await GetJsonAsync(golfer.Client, "/api/stats");
        Assert.Equal(0, stats.GetProperty("totalRounds").GetInt32());
    }
}
