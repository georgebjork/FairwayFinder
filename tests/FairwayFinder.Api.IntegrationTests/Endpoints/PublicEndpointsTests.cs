using System.Net;
using System.Text.Json;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.IntegrationTests.Common;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

/// <summary>The small anonymous web surface: the Apple association file and the deep-link landing pages.</summary>
public class PublicEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Apple_app_site_association_routes_register_and_reset_links_into_the_app()
    {
        using var client = AnonymousClient();

        var response = await client.GetAsync("/.well-known/apple-app-site-association");

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var paths = json.GetProperty("applinks").GetProperty("details")[0].GetProperty("components")
            .EnumerateArray().Select(c => c.GetProperty("/").GetString()).ToList();
        Assert.Contains("/register*", paths);
        Assert.Contains("/reset-password*", paths);
        Assert.True(json.TryGetProperty("webcredentials", out _));
    }

    [Theory]
    [InlineData("/register?code=abc")]
    [InlineData("/reset-password?email=a%40b.c&token=xyz")]
    public async Task Landing_pages_render_html_with_the_install_link(string url)
    {
        using var client = AnonymousClient();

        var response = await client.GetAsync(url);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(TestSettings.AppInstallUrl, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_api_routes_are_not_found()
    {
        var golfer = await SignInNewUserAsync();
        await AssertStatusAsync(HttpStatusCode.NotFound, await golfer.Client.GetAsync("/api/does-not-exist"));
    }
}
