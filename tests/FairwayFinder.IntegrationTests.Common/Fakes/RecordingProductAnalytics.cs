using System.Collections.Concurrent;
using FairwayFinder.Features.Services.Analytics;

namespace FairwayFinder.IntegrationTests.Common.Fakes;

public sealed record CapturedEvent(
    string UserId,
    string EventName,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyDictionary<string, object?> PersonProperties);

/// <summary>
/// Captures product analytics events instead of sending them to PostHog, so tests can assert
/// that what a golfer did was recorded, under their id, with the right facts.
/// </summary>
public sealed class RecordingProductAnalytics : IProductAnalytics
{
    private readonly ConcurrentQueue<CapturedEvent> _captured = new();

    public IReadOnlyList<CapturedEvent> Captured => _captured.ToArray();

    public void Clear() => _captured.Clear();

    public IReadOnlyList<CapturedEvent> For(string userId, string eventName) =>
        Captured.Where(e => e.UserId == userId && e.EventName == eventName).ToList();

    public void Capture(
        string userId,
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null,
        IReadOnlyDictionary<string, object?>? personProperties = null)
    {
        _captured.Enqueue(new CapturedEvent(
            userId,
            eventName,
            properties ?? new Dictionary<string, object?>(),
            personProperties ?? new Dictionary<string, object?>()));
    }
}
