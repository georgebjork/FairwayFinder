using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PostHog;

namespace FairwayFinder.Features.Services.Analytics;

/// <summary>
/// Sends events through the PostHog SDK, which queues them and flushes in batches on its own
/// timer — and once more when the container disposes it at shutdown.
/// </summary>
public sealed class PostHogProductAnalytics(
    IPostHogClient client,
    IHostEnvironment environment,
    ILogger<PostHogProductAnalytics> logger) : IProductAnalytics
{
    public void Capture(
        string userId,
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null,
        IReadOnlyDictionary<string, object?>? personProperties = null)
    {
        var eventProperties = new Dictionary<string, object>
        {
            // Which host raised it. Today only the API emits events, but an admin-side event
            // should never be mistaken for something a golfer did in the app.
            ["app"] = environment.ApplicationName
        };

        if (properties is not null)
        {
            foreach (var (key, value) in properties)
            {
                if (value is not null) eventProperties[key] = value;
            }
        }

        if (personProperties is not null)
        {
            eventProperties["$set"] = personProperties
                .Where(p => p.Value is not null)
                .ToDictionary(p => p.Key, p => p.Value!);
        }

        // Analytics must never fail the action it describes.
        try
        {
            client.Capture(userId, eventName, eventProperties);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not queue analytics event {EventName}", eventName);
        }
    }
}
