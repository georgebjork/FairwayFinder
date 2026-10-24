using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using FairwayFinder.Api.IntegrationTests.Infrastructure;

namespace FairwayFinder.Api.IntegrationTests.Logging;

/// <summary>
/// The phone, not the API, is the caller: whatever <c>traceparent</c> a client SDK sends, every
/// request is recorded and is the root of its own trace. Honouring the header either dropped the
/// request span entirely (unsampled "-00") or orphaned it under a parent that never reaches the
/// backend.
/// </summary>
public class TracingTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string CallerTraceId = "0af7651916cd43dd8448eb211c80319c";

    [Theory]
    [InlineData("00")]
    [InlineData("01")]
    public async Task A_request_is_recorded_as_its_own_root_span_whatever_traceparent_the_client_sends(string flags)
    {
        var me = await SignInNewUserAsync();

        var stopped = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            // Abstain, so the app's own sampler alone decides what is recorded.
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.None,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/friends/");
        request.Headers.Add("traceparent", $"00-{CallerTraceId}-b7ad6b7169203331-{flags}");
        var response = await me.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The server span stops just after the response is flushed, so it can trail the client.
        for (var i = 0; i < 50 && !stopped.Any(a => a.DisplayName == "GET /api/friends/"); i++)
            await Task.Delay(20);

        var span = Assert.Single(stopped, a => a.DisplayName == "GET /api/friends/");
        Assert.True(span.Recorded);
        Assert.Null(span.ParentId);
        Assert.DoesNotContain(stopped, a => a.TraceId.ToHexString() == CallerTraceId);
    }
}
