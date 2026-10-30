using System.Net;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Services.Analytics;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

/// <summary>
/// <c>app_active</c>: one event per golfer per day however many requests they make, so active
/// golfer charts count people rather than traffic.
/// </summary>
public class ActiveGolferTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Logging_in_and_using_the_app_records_one_active_day()
    {
        var golfer = await SignInNewUserAsync();

        await AssertStatusAsync(HttpStatusCode.NoContent, await golfer.Client.GetAsync("/api/rounds/active"));
        await AssertStatusAsync(HttpStatusCode.OK, await golfer.Client.GetAsync("/api/rounds"));
        await AssertStatusAsync(HttpStatusCode.OK, await golfer.Client.GetAsync("/api/profile"));

        Assert.Single(Analytics.For(golfer.Id, AnalyticsEvents.AppActive));
    }

    [Fact]
    public async Task Anonymous_requests_record_nothing()
    {
        using var client = AnonymousClient();

        await client.GetAsync("/api/rounds");

        Assert.DoesNotContain(Analytics.Captured, e => e.EventName == AnalyticsEvents.AppActive);
    }
}
