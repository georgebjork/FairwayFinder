using System.Collections.Concurrent;

namespace FairwayFinder.Features.Services.Analytics;

/// <summary>
/// Records <see cref="AnalyticsEvents.AppActive"/> the first time a golfer is seen on each UTC day,
/// so daily and weekly active golfers can be counted without an event per request.
/// </summary>
/// <remarks>
/// The memory is per process, so a restart or a second instance can send a second event for the
/// same day. That is harmless: active-golfer charts count unique people, not events. It holds one
/// entry per golfer, so it stays small.
/// </remarks>
public sealed class ActiveGolferTracker(IProductAnalytics analytics, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, DateOnly> _lastActive = new();

    public void MarkActive(string userId)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        // Every authenticated request reaches this; almost all of them stop here.
        if (_lastActive.TryGetValue(userId, out var last) && last == today) return;

        // AddOrUpdate re-runs a factory that lost a race against the value the winner wrote, so
        // when two requests open the day together only the one that moved it forward sends.
        var isNewDay = false;
        _lastActive.AddOrUpdate(
            userId,
            _ =>
            {
                isNewDay = true;
                return today;
            },
            (_, previous) =>
            {
                isNewDay = previous != today;
                return today;
            });

        if (isNewDay) analytics.Capture(userId, AnalyticsEvents.AppActive);
    }
}
