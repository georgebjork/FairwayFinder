using System.Security.Claims;
using FairwayFinder.Features.Services.Analytics;

namespace FairwayFinder.Api.Middleware;

/// <summary>
/// Marks the signed-in golfer active for today on any authenticated request, which sends
/// <see cref="AnalyticsEvents.AppActive"/> once per golfer per day. Hooked to requests rather than
/// to token refresh because the app only refreshes when its access token has expired, so a golfer
/// who opens the app twice either side of midnight would otherwise not count on the second day.
/// </summary>
public static class ActiveGolferMiddleware
{
    /// <summary>Register after <c>UseAuthentication</c>, so the golfer is known.</summary>
    public static WebApplication UseActiveGolferTracking(this WebApplication app)
    {
        var tracker = app.Services.GetRequiredService<ActiveGolferTracker>();

        app.Use(async (context, next) =>
        {
            // Read the claim directly: GetUserId throws when it is missing, and analytics must
            // never fail a request.
            if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId)
            {
                tracker.MarkActive(userId);
            }

            await next(context);
        });

        return app;
    }
}
