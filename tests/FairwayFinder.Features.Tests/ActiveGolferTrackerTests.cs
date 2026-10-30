using FairwayFinder.Features.Services.Analytics;

namespace FairwayFinder.Features.Tests;

public class ActiveGolferTrackerTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Counting : IProductAnalytics
    {
        public List<(string UserId, string EventName)> Events { get; } = [];

        public void Capture(string userId, string eventName,
            IReadOnlyDictionary<string, object?>? properties = null,
            IReadOnlyDictionary<string, object?>? personProperties = null)
            => Events.Add((userId, eventName));
    }

    [Fact]
    public void A_golfer_counts_once_per_utc_day()
    {
        var clock = new Clock(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));
        var analytics = new Counting();
        var tracker = new ActiveGolferTracker(analytics, clock);

        tracker.MarkActive("golfer-1");
        tracker.MarkActive("golfer-1");
        clock.Now = clock.Now.AddHours(15); // 23:00, same day
        tracker.MarkActive("golfer-1");

        Assert.Single(analytics.Events);

        clock.Now = clock.Now.AddHours(2); // 01:00 the next day
        tracker.MarkActive("golfer-1");

        Assert.Equal(2, analytics.Events.Count);
        Assert.All(analytics.Events, e => Assert.Equal(AnalyticsEvents.AppActive, e.EventName));
    }

    [Fact]
    public void Each_golfer_counts_separately()
    {
        var analytics = new Counting();
        var tracker = new ActiveGolferTracker(analytics, new Clock(DateTimeOffset.UtcNow));

        tracker.MarkActive("golfer-1");
        tracker.MarkActive("golfer-2");

        Assert.Equal(["golfer-1", "golfer-2"], analytics.Events.Select(e => e.UserId));
    }

    [Fact]
    public async Task Concurrent_first_requests_of_the_day_send_one_event()
    {
        var analytics = new ConcurrentCounting();
        var tracker = new ActiveGolferTracker(analytics, new Clock(DateTimeOffset.UtcNow));

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() => tracker.MarkActive("golfer-1"))));

        Assert.Equal(1, analytics.Count);
    }

    private sealed class ConcurrentCounting : IProductAnalytics
    {
        private int _count;
        public int Count => _count;

        public void Capture(string userId, string eventName,
            IReadOnlyDictionary<string, object?>? properties = null,
            IReadOnlyDictionary<string, object?>? personProperties = null)
            => Interlocked.Increment(ref _count);
    }
}
