using System.Net;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using static FairwayFinder.Api.IntegrationTests.Infrastructure.RoundFlows;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

/// <summary>Stats aggregate only the caller's posted, counted rounds.</summary>
public class StatsEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Overall_stats_average_posted_rounds()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, course, overPar: 0);
        await PlayCompletedRoundAsync(golfer.Client, course, overPar: 1);

        var stats = await GetAsync<UserStatsResponse>(golfer.Client, "/api/stats");

        Assert.Equal(2, stats.TotalRounds);
        Assert.Equal(2, stats.Scoring.Rounds18Hole);
        Assert.Equal(course.Par + 9, stats.Scoring.Average18HoleScore);
        Assert.Equal(course.Par, stats.Scoring.Best18HoleRound?.Score);
    }

    [Fact]
    public async Task Stats_with_no_rounds_are_empty_not_an_error()
    {
        var golfer = await SignInNewUserAsync();

        var stats = await GetAsync<UserStatsResponse>(golfer.Client, "/api/stats");

        Assert.Equal(0, stats.TotalRounds);
    }

    [Fact]
    public async Task Stats_filter_by_year_and_course()
    {
        var golfer = await SignInNewUserAsync();
        var home = await Data.CreateCourseAsync();
        var away = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, home, datePlayed: new DateOnly(2025, 6, 1));
        await PlayCompletedRoundAsync(golfer.Client, home, datePlayed: new DateOnly(2026, 6, 1));
        await PlayCompletedRoundAsync(golfer.Client, away, datePlayed: new DateOnly(2026, 6, 2));

        var in2026 = await GetAsync<UserStatsResponse>(golfer.Client, "/api/stats?year=2026");
        var atHome = await GetAsync<UserStatsResponse>(golfer.Client, $"/api/stats?courseId={home.CourseId}");

        Assert.Equal(2, in2026.TotalRounds);
        Assert.Equal(2, atHome.TotalRounds);
    }

    [Fact]
    public async Task Stats_accept_an_explicit_strokes_gained_level()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, course);

        await AssertStatusAsync(HttpStatusCode.OK, await golfer.Client.GetAsync("/api/stats?level=Hcp10"));
        await AssertStatusAsync(HttpStatusCode.BadRequest, await golfer.Client.GetAsync("/api/stats?level=NotALevel"));
    }

    [Fact]
    public async Task Course_and_hole_stats_cover_rounds_at_that_course()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync("Stats Test Course");
        await PlayCompletedRoundAsync(golfer.Client, course, overPar: 1);

        var courseStats = await GetAsync<CourseStatsResponse>(golfer.Client, $"/api/stats/courses/{course.CourseId}");
        Assert.Equal(course.CourseId, courseStats.CourseId);
        Assert.Equal(1, courseStats.TotalRounds);

        var holeStats = await GetJsonAsync(golfer.Client, $"/api/stats/courses/{course.CourseId}/holes");
        Assert.Equal(course.CourseId, holeStats.GetProperty("courseId").GetInt64());
    }

    [Fact]
    public async Task Course_stats_for_an_unknown_course_is_not_found()
    {
        var golfer = await SignInNewUserAsync();
        await AssertStatusAsync(HttpStatusCode.NotFound, await golfer.Client.GetAsync("/api/stats/courses/987654321"));
    }

    [Fact]
    public async Task Years_reflect_rounds_played()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, course, datePlayed: new DateOnly(2024, 4, 1));
        await PlayCompletedRoundAsync(golfer.Client, course, datePlayed: new DateOnly(2026, 4, 1));

        var years = await GetAsync<List<int>>(golfer.Client, "/api/stats/years");

        Assert.Equal([2026, 2024], years);
    }

    [Fact]
    public async Task Stat_courses_only_list_courses_with_hole_stat_rounds()
    {
        var golfer = await SignInNewUserAsync();
        var scoreOnly = await Data.CreateCourseAsync();
        var tracked = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, scoreOnly);

        var request = StartRequest(tracked);
        request.UsingHoleStats = true;
        var started = await StartRoundAsync(golfer.Client, request);
        foreach (var hole in started.Holes)
            await UpsertHoleAsync(golfer.Client, started.RoundId, hole.HoleNumber, (short)hole.Par);
        await AssertStatusAsync(HttpStatusCode.OK, await golfer.Client.PostAsync($"/api/rounds/{started.RoundId}/complete", null));

        var courses = await GetJsonAsync(golfer.Client, "/api/stats/courses");

        var course = Assert.Single(courses.EnumerateArray());
        Assert.Equal(tracked.CourseId, course.GetProperty("courseId").GetInt64());
    }
}
