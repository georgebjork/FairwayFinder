namespace FairwayFinder.Features.Services.Analytics;

/// <summary>Used when no PostHog project token is configured: local dev, tests, any opt-out.</summary>
public sealed class NullProductAnalytics : IProductAnalytics
{
    public static readonly NullProductAnalytics Instance = new();

    public void Capture(
        string userId,
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null,
        IReadOnlyDictionary<string, object?>? personProperties = null)
    {
    }
}
