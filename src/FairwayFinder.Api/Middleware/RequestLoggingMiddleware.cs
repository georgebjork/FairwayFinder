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
/// <see cref="BackgroundServices.RequestLogWriter"/>. Logging never affects the response —
/// failures are swallowed and channel overflow drops the entry.
/// </summary>
/// <remarks>
/// Runs inside <c>UseExceptionHandler</c>, so when an endpoint throws, the status is still the
/// default 200 when <c>next</c> returns here; <c>GlobalExceptionHandler</c> writes the real one
/// afterwards. The entry is therefore written from <c>Response.OnCompleted</c>, once the final
/// status is on the response. The matched endpoint is captured on the way out instead, because the
/// exception handler clears it before it writes the error.
/// </remarks>
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
        string? routeTemplate = null;

        context.Response.OnCompleted(() =>
        {
            TryLog(context, routeTemplate, Stopwatch.GetElapsedTime(start));
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            routeTemplate = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
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

    private void TryLog(HttpContext context, string? routeTemplate, TimeSpan elapsed)
    {
        try
        {
            var user = context.User;
            var entry = new ApiRequestLog
            {
                Timestamp = DateTime.UtcNow,
                Method = context.Request.Method,
                Path = Truncate(context.Request.Path.Value, 2048) ?? string.Empty,
                RouteTemplate = routeTemplate,
                QueryString = context.Request.QueryString.HasValue
                    ? Truncate(context.Request.QueryString.Value, 2048)
                    : null,
                StatusCode = context.Response.StatusCode,
                DurationMs = (int)elapsed.TotalMilliseconds,
                UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                UserEmail = user.FindFirstValue(ClaimTypes.Email),
                // Already the client's address: UseForwardedHeaders runs first and takes it from the
                // proxy's X-Forwarded-For entry (the left-most one is whatever the client claimed).
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
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

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
