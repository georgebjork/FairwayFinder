using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using FairwayFinder.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using static FairwayFinder.Api.IntegrationTests.Infrastructure.RoundFlows;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

/// <summary>Reading, editing, excluding and deleting posted rounds, and who may do each.</summary>
public class RoundEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Posted_rounds_are_listed_with_course_and_score()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync("Pebble Test Links");
        await PlayCompletedRoundAsync(golfer.Client, course, overPar: 1);

        var rounds = await GetAsync<List<RoundResponse>>(golfer.Client, "/api/rounds");

        var round = Assert.Single(rounds);
        Assert.Equal(course.Par + 18, round.Score);
        Assert.Equal("Pebble Test Links", round.CourseName);
        Assert.True(round.IsComplete);
    }

    [Fact]
    public async Task Round_list_honours_course_and_date_filters()
    {
        var golfer = await SignInNewUserAsync();
        var home = await Data.CreateCourseAsync();
        var away = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, home, datePlayed: new DateOnly(2025, 5, 1));
        await PlayCompletedRoundAsync(golfer.Client, home, datePlayed: new DateOnly(2026, 5, 1));
        await PlayCompletedRoundAsync(golfer.Client, away, datePlayed: new DateOnly(2026, 6, 1));

        var atHome = await GetAsync<List<RoundResponse>>(golfer.Client, $"/api/rounds?courseId={home.CourseId}");
        var in2026 = await GetAsync<List<RoundResponse>>(golfer.Client, "/api/rounds?startDate=2026-01-01&endDate=2026-12-31");

        Assert.Equal(2, atHome.Count);
        Assert.Equal(2, in2026.Count);
    }

    [Fact]
    public async Task Golfers_only_see_their_own_rounds()
    {
        var me = await SignInNewUserAsync();
        var someoneElse = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(someoneElse.Client, course);

        var rounds = await GetJsonAsync(me.Client, "/api/rounds");

        Assert.Equal(0, rounds.GetArrayLength());
    }

    [Fact]
    public async Task Round_by_id_returns_every_hole()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await PlayCompletedRoundAsync(golfer.Client, course);

        var round = await GetAsync<RoundResponse>(golfer.Client, $"/api/rounds/{roundId}");

        Assert.Equal(roundId, round.RoundId);
        Assert.Equal(18, round.Holes.Count);
        Assert.Equal(course.Par, round.Score);
    }

    [Fact]
    public async Task Another_golfers_round_is_forbidden_and_a_missing_round_is_not_found()
    {
        var owner = await SignInNewUserAsync();
        var intruder = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await PlayCompletedRoundAsync(owner.Client, course);

        await AssertStatusAsync(HttpStatusCode.Forbidden, await intruder.Client.GetAsync($"/api/rounds/{roundId}"));
        await AssertStatusAsync(HttpStatusCode.Forbidden, await intruder.Client.DeleteAsync($"/api/rounds/{roundId}"));
        await AssertStatusAsync(HttpStatusCode.Forbidden, await intruder.Client.GetAsync($"/api/rounds/{roundId}/shots"));
        await AssertStatusAsync(HttpStatusCode.NotFound, await owner.Client.GetAsync("/api/rounds/987654321"));
    }

    [Fact]
    public async Task Round_details_include_hole_data_and_respect_exclusion()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var counted = await PlayCompletedRoundAsync(golfer.Client, course);
        var excluded = await PlayCompletedRoundAsync(golfer.Client, course, overPar: 2);

        await AssertStatusAsync(HttpStatusCode.NoContent, await golfer.Client.PatchAsJsonAsync(
            $"/api/rounds/{excluded}/exclude-from-stats", new SetExcludeFromStatsRequest { ExcludeFromStats = true }));

        var details = await GetAsync<List<RoundResponse>>(golfer.Client, "/api/rounds/details");

        var round = Assert.Single(details);
        Assert.Equal(counted, round.RoundId);
        Assert.Equal(18, round.Holes.Count);

        // Excluded rounds still appear in the plain list, flagged.
        var list = await GetAsync<List<RoundResponse>>(golfer.Client, "/api/rounds");
        Assert.True(list.Single(r => r.RoundId == excluded).ExcludeFromStats);
    }

    [Fact]
    public async Task Atomic_submit_creates_a_posted_round()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var holes = await GetAsync<List<HoleInfo>>(golfer.Client, $"/api/courses/teeboxes/{course.TeeboxId}/holes");

        var response = await golfer.Client.PostAsJsonAsync("/api/rounds", new CreateRoundRequest
        {
            CourseId = course.CourseId,
            TeeboxId = course.TeeboxId,
            DatePlayed = DefaultDate,
            FullRound = true,
            Holes = holes.Select(h => new HoleScoreEntry
            {
                HoleId = h.HoleId,
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Score = (short)(h.Par + 1),
            }).ToList(),
        });

        var roundId = await ReadAsync<long>(response, HttpStatusCode.Created);
        var round = await GetAsync<RoundResponse>(golfer.Client, $"/api/rounds/{roundId}");
        Assert.Equal(course.Par + 18, round.Score);
        Assert.True(round.IsComplete);
    }

    [Fact]
    public async Task Atomic_submit_without_holes_is_a_validation_error()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();

        var response = await golfer.Client.PostAsJsonAsync("/api/rounds", new CreateRoundRequest
        {
            CourseId = course.CourseId,
            TeeboxId = course.TeeboxId,
            DatePlayed = DefaultDate,
            FullRound = true,
        });

        var problem = await ReadAsync<JsonElement>(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Holes", out _));
    }

    [Fact]
    public async Task Editing_a_posted_round_replaces_its_scores()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await PlayCompletedRoundAsync(golfer.Client, course);
        var round = await GetAsync<RoundResponse>(golfer.Client, $"/api/rounds/{roundId}");

        // The validator runs before the route value is copied in, so the body has to repeat the id —
        // which is what the iOS client sends.
        var response = await golfer.Client.PutAsJsonAsync($"/api/rounds/{roundId}", new UpdateRoundRequest
        {
            RoundId = roundId,
            TeeboxId = course.TeeboxId,
            DatePlayed = new DateOnly(2026, 8, 15),
            Holes = round.Holes.Select(h => new HoleScoreEntry
            {
                ScoreId = h.ScoreId,
                HoleId = h.HoleId,
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Score = (short)(h.Par + 1),
            }).ToList(),
        });
        await AssertStatusAsync(HttpStatusCode.NoContent, response);

        var updated = await GetAsync<RoundResponse>(golfer.Client, $"/api/rounds/{roundId}");
        Assert.Equal(course.Par + 18, updated.Score);
        Assert.Equal(new DateOnly(2026, 8, 15), updated.DatePlayed);

        // Edited in place: still one live score per hole.
        await using var db = Db();
        Assert.Equal(18, await db.Scores.CountAsync(s => s.RoundId == roundId && !s.IsDeleted));
    }

    [Fact]
    public async Task Editing_a_round_still_in_progress_conflicts()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var started = await StartRoundAsync(golfer.Client, StartRequest(course));

        var response = await golfer.Client.PutAsJsonAsync($"/api/rounds/{started.RoundId}", new UpdateRoundRequest
        {
            RoundId = started.RoundId,
            TeeboxId = course.TeeboxId,
            DatePlayed = DefaultDate,
            Holes = started.Holes.Select(h => new HoleScoreEntry
            {
                HoleId = h.HoleId, HoleNumber = h.HoleNumber, Par = h.Par, Score = (short)h.Par,
            }).ToList(),
        });

        await AssertStatusAsync(HttpStatusCode.Conflict, response);
    }

    [Fact]
    public async Task Deleting_a_round_soft_deletes_it_and_its_scores()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await PlayCompletedRoundAsync(golfer.Client, course);

        await AssertStatusAsync(HttpStatusCode.NoContent, await golfer.Client.DeleteAsync($"/api/rounds/{roundId}"));

        await AssertStatusAsync(HttpStatusCode.NotFound, await golfer.Client.GetAsync($"/api/rounds/{roundId}"));
        Assert.Equal(0, (await GetJsonAsync(golfer.Client, "/api/rounds")).GetArrayLength());

        await using var db = Db();
        Assert.True((await db.Rounds.SingleAsync(r => r.RoundId == roundId)).IsDeleted);
        Assert.False(await db.Scores.AnyAsync(s => s.RoundId == roundId && !s.IsDeleted));
    }

    [Fact]
    public async Task Discarding_an_open_round_lets_a_new_one_start()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var abandoned = await StartRoundAsync(golfer.Client, StartRequest(course));

        await AssertStatusAsync(HttpStatusCode.NoContent, await golfer.Client.DeleteAsync($"/api/rounds/{abandoned.RoundId}"));

        // ix_round_user_id_active filters out deleted rounds, so the slot is free again.
        var next = await StartRoundAsync(golfer.Client, StartRequest(course));
        Assert.NotEqual(abandoned.RoundId, next.RoundId);
    }

    [Fact]
    public async Task Shots_for_a_round_without_shot_tracking_are_empty()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await PlayCompletedRoundAsync(golfer.Client, course);

        var shots = await GetJsonAsync(golfer.Client, $"/api/rounds/{roundId}/shots");

        // Keyed by score id; no shot-tracked holes means no keys.
        Assert.Equal(JsonValueKind.Object, shots.ValueKind);
        Assert.Empty(shots.EnumerateObject());
    }

    [Fact]
    public async Task Played_courses_lists_each_course_once()
    {
        var golfer = await SignInNewUserAsync();
        var home = await Data.CreateCourseAsync("Home Course");
        var away = await Data.CreateCourseAsync("Away Course");
        await PlayCompletedRoundAsync(golfer.Client, home);
        await PlayCompletedRoundAsync(golfer.Client, home);
        await PlayCompletedRoundAsync(golfer.Client, away);

        var courses = await GetJsonAsync(golfer.Client, "/api/rounds/courses");

        Assert.Equal(2, courses.GetArrayLength());
    }
}
