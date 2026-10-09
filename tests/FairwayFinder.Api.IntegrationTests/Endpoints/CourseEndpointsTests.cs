using System.Net;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

public class CourseEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Search_matches_course_names_case_insensitively()
    {
        var golfer = await SignInNewUserAsync();
        await Data.CreateCourseAsync("Augusta Test National");
        await Data.CreateCourseAsync("St Andrews Test Old");

        var results = await GetAsync<List<CourseSearchResult>>(golfer.Client, "/api/courses/search?query=augusta");

        var match = Assert.Single(results);
        Assert.Equal("Augusta Test National", match.CourseName);
    }

    [Fact]
    public async Task Search_without_a_query_parameter_is_a_bad_request()
    {
        var golfer = await SignInNewUserAsync();
        await AssertStatusAsync(HttpStatusCode.BadRequest, await golfer.Client.GetAsync("/api/courses/search"));
    }

    [Fact]
    public async Task Teeboxes_and_holes_describe_the_course()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();

        var teeboxes = await GetAsync<List<TeeboxOption>>(golfer.Client, $"/api/courses/{course.CourseId}/teeboxes");
        var teebox = Assert.Single(teeboxes);
        Assert.Equal(course.TeeboxId, teebox.TeeboxId);
        Assert.Equal(72, teebox.Par);

        var holes = await GetAsync<List<HoleInfo>>(golfer.Client, $"/api/courses/teeboxes/{course.TeeboxId}/holes");
        Assert.Equal(18, holes.Count);
        Assert.Equal(course.Pars, holes.OrderBy(h => h.HoleNumber).Select(h => h.Par));
    }

    [Fact]
    public async Task Teeboxes_can_be_filtered_by_the_golfers_preferred_tees()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();

        var response = await golfer.Client.GetAsync($"/api/courses/{course.CourseId}/teeboxes?usePreferredTees=true");

        await AssertStatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task Course_endpoints_require_sign_in()
    {
        using var client = AnonymousClient();
        await AssertStatusAsync(HttpStatusCode.Unauthorized, await client.GetAsync("/api/courses/search?query=x"));
    }
}
