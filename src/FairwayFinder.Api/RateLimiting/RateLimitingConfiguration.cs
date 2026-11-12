using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace FairwayFinder.Api.RateLimiting;

public static class RateLimitingConfiguration
{
    /// <summary>Policy name for the <c>/api/auth</c> group.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Policy name for the anonymous <c>/api/public</c> group (public stats pages).</summary>
    public const string PublicProfilePolicy = "public-profile";

    /// <summary>
    /// Trusts <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> from the TLS-terminating proxy in
    /// front of the container, so <c>Connection.RemoteIpAddress</c> is the golfer's IP rather than
    /// the proxy's. Without it every client shares one rate-limit bucket.
    /// </summary>
    /// <remarks>
    /// App Platform puts two proxies in front of the container, Cloudflare and then DigitalOcean's
    /// load balancer, and each appends to the header: <c>[client-written...], client, cloudflare-edge</c>.
    /// <c>ForwardLimit</c> 2 walks back past the edge to the address Cloudflare appended, which is the
    /// golfer. Entries a client writes itself sit further left and are ignored. The proxies' addresses
    /// aren't known to the app, so any sender is trusted; that holds only while the container is
    /// reachable solely through App Platform. If the number of proxies changes, change the limit.
    /// </remarks>
    public static IServiceCollection AddFairwayFinderForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2;
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
            options.AddPolicy(PublicProfilePolicy, context => Partition(settings, settings.PublicProfile, ClientIp(context)));

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
