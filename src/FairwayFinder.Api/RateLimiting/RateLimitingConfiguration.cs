using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace FairwayFinder.Api.RateLimiting;

public static class RateLimitingConfiguration
{
    /// <summary>Policy name for the <c>/api/auth</c> group.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>
    /// Trusts <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> from the TLS-terminating proxy in
    /// front of the container, so <c>Connection.RemoteIpAddress</c> is the golfer's IP rather than
    /// the proxy's. Without it every client shares one rate-limit bucket.
    /// </summary>
    /// <remarks>
    /// The proxy's address isn't known to the app, so any sender is trusted, but with the default
    /// <c>ForwardLimit</c> of 1 only the last entry is used: the one the proxy appended. Entries a
    /// client writes itself sit to its left and are ignored. That holds only while the container is
    /// reachable solely through the proxy.
    /// </remarks>
    public static IServiceCollection AddFairwayFinderForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

    public static IServiceCollection AddFairwayFinderRateLimiting(this IServiceCollection services, RateLimitingSettings settings)
    {
        services.AddRateLimiter(options =>
        {
            // Every request draws from one of two global buckets, keyed by IP: matched endpoints
            // share a generous one, and paths no endpoint answers get a small, separate one, so a
            // scanner shuts itself out without touching that IP's real API budget.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.GetEndpoint() is null
                    ? Partition(settings, settings.Unmatched, $"unmatched:{ClientIp(context)}")
                    : Partition(settings, settings.Api, $"api:{ClientIp(context)}"));

            options.AddPolicy(AuthPolicy, context => Partition(settings, settings.Auth, ClientIp(context)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too many requests",
                        detail: "Too many requests from this address. Please wait and try again.")
                    .ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> Partition(RateLimitingSettings settings, RateLimitWindow window, string key) =>
        settings.Enabled
            ? RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = window.PermitLimit,
                Window = TimeSpan.FromSeconds(window.WindowSeconds),
                QueueLimit = 0
            })
            : RateLimitPartition.GetNoLimiter(key);

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
