using System.Net;
using FairwayFinder.Api.BackgroundServices;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Data.Entities;
using FairwayFinder.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Api.IntegrationTests.Logging;

/// <summary>
/// The api_request_log pipeline end to end. Request logging and its writer are off in the shared
/// test host (the writer would race the per-test reset), so each test switches logging on and runs
/// its own writer for just its own duration. A second host (WithWebHostBuilder) is not an option:
/// once disposed it leaves the shared host's app_log sink silent, failing AppLogTests.
/// </summary>
public class RequestLogTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task A_request_that_throws_is_logged_with_the_status_the_client_received()
    {
        var golfer = await SignInNewUserAsync();

        await using (await RequestLoggingOnAsync())
        {
            // GetRoundById throws NotFoundException, which GlobalExceptionHandler turns into a 404.
            var response = await golfer.Client.GetAsync("/api/rounds/987654321");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var entry = await WaitForAsync(l => l.Path == "/api/rounds/987654321");
            Assert.Equal(404, entry.StatusCode);
            Assert.Equal(golfer.Id, entry.UserId);
            Assert.Equal("/api/rounds/{roundId:long}", entry.RouteTemplate);
        }
    }

    private async Task<IAsyncDisposable> RequestLoggingOnAsync()
    {
        var settings = Services.GetRequiredService<IOptions<RequestLoggingSettings>>().Value;
        var writer = ActivatorUtilities.CreateInstance<RequestLogWriter>(Services);

        settings.Enabled = true;
        await writer.StartAsync(CancellationToken.None);

        return new Restore(async () =>
        {
            settings.Enabled = false;
            await writer.StopAsync(CancellationToken.None);
            writer.Dispose();
        });
    }

    private async Task<ApiRequestLog> WaitForAsync(System.Linq.Expressions.Expression<Func<ApiRequestLog, bool>> match)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = LogDb();
            var entry = await db.ApiRequestLogs.FirstOrDefaultAsync(match);
            if (entry is not null) return entry;

            await Task.Delay(100);
        }

        throw new TimeoutException("No matching api_request_log row was written within 15s.");
    }

    private sealed class Restore(Func<Task> restore) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await restore();
    }
}
