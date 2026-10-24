using System.Net.Http.Json;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace FairwayFinder.Api.IntegrationTests.Logging;

/// <summary>
/// The Serilog database sink, end to end: real host, real Serilog pipeline, real logs database.
/// Writes are batched in the background, so each test waits for a sentinel event to land rather
/// than sleeping, and asserts on its own markers rather than on an empty table.
/// </summary>
public class AppLogTests(ApiFactory factory) : ApiTestBase(factory)
{
    private ILogger Logger => Services.GetRequiredService<ILogger<AppLogTests>>();

    [Fact]
    public async Task A_warning_is_stored_with_its_stack_trace_and_the_user_it_happened_under()
    {
        var marker = Guid.NewGuid().ToString("N");

        using (LogContext.PushProperty("UserId", "user-123"))
        using (LogContext.PushProperty("UserEmail", "golfer@test.fairwayfinder.pro"))
        {
            Logger.LogWarning(Thrown(), "Something odd happened {Marker} on hole {HoleNumber}", marker, 7);
        }

        var entry = await WaitForAsync(marker);

        Assert.Equal("Warning", entry.Level);
        Assert.Equal($"Something odd happened {marker} on hole 7", entry.Message);
        Assert.Equal("Something odd happened {Marker} on hole {HoleNumber}", entry.MessageTemplate);
        Assert.Equal("user-123", entry.UserId);
        Assert.Equal("golfer@test.fairwayfinder.pro", entry.UserEmail);
        Assert.Equal(typeof(AppLogTests).FullName, entry.SourceContext);
        Assert.Equal("FairwayFinder.Api", entry.Application);
        Assert.Contains("InvalidOperationException: boom", entry.Exception);
        Assert.Contains(nameof(Thrown), entry.Exception);
        using var properties = System.Text.Json.JsonDocument.Parse(entry.Properties!);
        Assert.Equal(7, properties.RootElement.GetProperty("HoleNumber").GetInt32());
    }

    [Fact]
    public async Task Information_is_not_stored()
    {
        var info = Guid.NewGuid().ToString("N");
        var sentinel = Guid.NewGuid().ToString("N");

        Logger.LogInformation("Routine event {Marker}", info);
        Logger.LogWarning("Sentinel {Marker}", sentinel);
        await WaitForAsync(sentinel);

        await using var db = LogDb();
        Assert.False(await db.AppLogs.AnyAsync(l => l.Message.Contains(info)));
    }

    [Fact]
    public async Task An_error_inside_a_request_is_attributed_to_the_signed_in_user()
    {
        var admin = await SignInNewAdminAsync();
        var golfer = await SignInNewUserAsync();
        await RegisterDeviceAsync(golfer);
        Apns.FailWith = new HttpRequestException("APNs unreachable");

        await admin.Client.PostAsJsonAsync("/api/devices/test",
            new SendTestPushRequest { TargetUserId = golfer.Id, Title = "Hello", Body = "From the tests" });

        var entry = await WaitForAsync("Error sending push", l => l.UserId == admin.Id);

        Assert.Equal("Error", entry.Level);
        Assert.Equal(admin.Email, entry.UserEmail);
        Assert.Equal("/api/devices/test", entry.RequestPath);
        Assert.Contains("APNs unreachable", entry.Exception);
        Assert.NotNull(entry.TraceId);
    }

    [Fact]
    public async Task A_rejected_4xx_request_is_not_stored_as_an_error()
    {
        var golfer = await SignInNewUserAsync();

        var response = await golfer.Client.GetAsync("/api/rounds/987654321");
        Assert.InRange((int)response.StatusCode, 400, 499);

        var sentinel = Guid.NewGuid().ToString("N");
        Logger.LogWarning("Sentinel {Marker}", sentinel);
        await WaitForAsync(sentinel);

        await using var db = LogDb();
        Assert.False(await db.AppLogs.AnyAsync(l => l.UserId == golfer.Id));
    }

    private async Task<AppLogEntry> WaitForAsync(
        string messageFragment, System.Linq.Expressions.Expression<Func<AppLogEntry, bool>>? also = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = LogDb();
            var query = db.AppLogs.Where(l => l.Message.Contains(messageFragment));
            if (also is not null) query = query.Where(also);

            var entry = await query.FirstOrDefaultAsync();
            if (entry is not null) return entry;

            await Task.Delay(100);
        }

        throw new TimeoutException($"No app_log row containing '{messageFragment}' was written within 15s.");
    }

    // Thrown and caught so the exception carries a real stack trace.
    private static Exception Thrown()
    {
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
