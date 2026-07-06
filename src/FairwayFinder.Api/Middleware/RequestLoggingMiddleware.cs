using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Channels;
using FairwayFinder.Data.Entities;
using FairwayFinder.Shared.Settings;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Api.Middleware;

/// <summary>
/// Records metadata for every API request into a bounded channel drained by
/// <see cref="BackgroundServices.RequestLogWriter"/>. Runs as the outermost app middleware so it
/// observes the final status code and the authenticated principal on the way back out. Logging
/// never affects the response — failures are swallowed and channel overflow drops the entry.
/// </summary>
public class RequestLoggingMiddleware(
    Channel<ApiRequestLog> channel,
    IOptions<RequestLoggingSettings> options,
    ILogger<RequestLoggingMiddleware> logger) : IMiddleware
{
    private readonly RequestLoggingSettings _settings = options.Value;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!_settings.Enabled || IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            TryLog(context, Stopwatch.GetElapsedTime(start));
        }
    }

    private bool IsExcluded(PathString path)
    {
        foreach (var prefix in _settings.ExcludedPathPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void TryLog(HttpContext context, TimeSpan elapsed)
    {
        try
        {
            var user = context.User;
            var entry = new ApiRequestLog
            {
                Timestamp = DateTime.UtcNow,
                Method = context.Request.Method,
                Path = Truncate(context.Request.Path.Value, 2048) ?? string.Empty,
                RouteTemplate = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText,
                QueryString = context.Request.QueryString.HasValue
                    ? Truncate(context.Request.QueryString.Value, 2048)
                    : null,
                StatusCode = context.Response.StatusCode,
                DurationMs = (int)elapsed.TotalMilliseconds,
                UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                UserEmail = user.FindFirstValue(ClaimTypes.Email),
                IpAddress = ResolveIpAddress(context),
                UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 512),
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier
            };

            if (!channel.Writer.TryWrite(entry))
            {
                logger.LogWarning("Request log buffer full; dropped a log entry for {Method} {Path}",
                    entry.Method, entry.Path);
            }
        }
        catch (Exception ex)
        {
            // Logging must never disrupt the request.
            logger.LogError(ex, "Failed to enqueue request log entry");
        }
    }

    private static string? ResolveIpAddress(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return Truncate(forwarded.Split(',')[0].Trim(), 64);

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
