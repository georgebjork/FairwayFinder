using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PostHog;
using Serilog.Core;
using Serilog.Events;

namespace FairwayFinder.Features.Services.Analytics;

/// <summary>
/// Serilog sink that sends Error-or-worse events carrying an exception to PostHog Error Tracking.
/// Registered in DI as an <see cref="ILogEventSink"/>, which the host's <c>ReadFrom.Services</c> picks
/// up — so one sink covers API requests, admin Blazor circuits and background jobs alike.
/// </summary>
/// <remarks>
/// <c>GlobalExceptionHandler</c> logs a 4xx at Information and only a real failure at Error, so
/// client mistakes never reach Error Tracking.
/// </remarks>
public sealed class PostHogExceptionSink : ILogEventSink
{
    private readonly Lazy<IPostHogClient> _client;
    private readonly string _systemDistinctId;

    // Set while this thread is inside CaptureException, so anything the SDK logs on the way
    // cannot come straight back in.
    [ThreadStatic] private static bool _capturing;

    public PostHogExceptionSink(IServiceProvider services, IHostEnvironment environment)
        // Resolved on first use, not here: Serilog builds its logger — and this sink — while the
        // container is still resolving the logger factory, and the PostHog client needs that
        // factory. Taking it eagerly would be a circular dependency.
        : this(new Lazy<IPostHogClient>(services.GetRequiredService<IPostHogClient>), environment.ApplicationName)
    {
    }

    internal PostHogExceptionSink(Lazy<IPostHogClient> client, string applicationName)
    {
        _client = client;
        _systemDistinctId = $"system:{applicationName}";
    }

    public void Emit(LogEvent logEvent)
    {
        if (_capturing || !ShouldCapture(logEvent)) return;

        var userId = StringProperty(logEvent, "UserId");
        var properties = BuildProperties(logEvent, userId is null);

        _capturing = true;
        try
        {
            _client.Value.CaptureException(logEvent.Exception!, userId ?? _systemDistinctId, properties);
        }
        catch
        {
            // A sink must never throw back into the code that logged; the error is already on
            // the console and in app_log.
        }
        finally
        {
            _capturing = false;
        }
    }

    internal static bool ShouldCapture(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Error || logEvent.Exception is null) return false;

        // The SDK's own failures would only feed themselves.
        var source = StringProperty(logEvent, "SourceContext");
        return source is null || !source.StartsWith("PostHog", StringComparison.Ordinal);
    }

    internal static Dictionary<string, object> BuildProperties(LogEvent logEvent, bool anonymous)
    {
        var properties = new Dictionary<string, object>
        {
            ["log_level"] = logEvent.Level.ToString(),
            // The template, not the rendered text, so occurrences group together.
            ["log_message_template"] = logEvent.MessageTemplate.Text
        };

        foreach (var name in (string[])["SourceContext", "Application", "RouteTemplate", "RequestPath", "RequestMethod"])
        {
            if (StringProperty(logEvent, name) is { } value) properties[name] = value;
        }

        if (logEvent.TraceId is { } traceId) properties["trace_id"] = traceId.ToHexString();

        // Background jobs and anonymous requests have no golfer. Don't let the shared system id
        // become a PostHog person collecting every one of them.
        if (anonymous) properties["$process_person_profile"] = false;

        return properties;
    }

    private static string? StringProperty(LogEvent logEvent, string name)
        => logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: string s }
            ? s
            : null;
}
