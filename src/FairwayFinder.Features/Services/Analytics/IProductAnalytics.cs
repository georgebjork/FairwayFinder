namespace FairwayFinder.Features.Services.Analytics;

/// <summary>
/// Product analytics: what golfers do, as named events in PostHog. The distinct id is always the
/// golfer's Identity user id — the same id the iOS app passes to PostHog's <c>identify</c>, which
/// is what joins server and app events into one person.
/// </summary>
/// <remarks>
/// Capture is fire-and-forget. Events are queued and sent in batches in the background, so a slow
/// or unreachable PostHog never holds up a request.
/// </remarks>
public interface IProductAnalytics
{
    /// <param name="userId">The golfer who did it. Never an admin acting on their behalf.</param>
    /// <param name="eventName">One of <see cref="AnalyticsEvents"/>.</param>
    /// <param name="properties">Facts about this occurrence (course, score, game type).</param>
    /// <param name="personProperties">Facts about the golfer, stored on their PostHog person (<c>$set</c>).</param>
    void Capture(
        string userId,
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null,
        IReadOnlyDictionary<string, object?>? personProperties = null);
}
