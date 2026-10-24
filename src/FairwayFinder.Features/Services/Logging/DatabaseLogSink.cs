using FairwayFinder.Shared.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace FairwayFinder.Features.Services.Logging;

/// <summary>
/// Serilog sink that persists Warning-or-worse events to the logs database for the admin console.
/// Registered in DI as an <see cref="ILogEventSink"/>, which the host's <c>ReadFrom.Services</c> picks
/// up. <see cref="Emit"/> only filters and enqueues; the database write happens in batches on
/// Serilog's background loop, so logging never waits on Postgres.
/// </summary>
public sealed class DatabaseLogSink : ILogEventSink, IDisposable
{
    // Requests are already one row each in api_request_log, and a 5xx's exception is logged by the
    // exception handler — storing the summary line too would duplicate every failure.
    private static readonly HashSet<string> ExcludedSources =
        ["Serilog.AspNetCore.RequestLoggingMiddleware"];

    private readonly LogEventLevel _minimumLevel;
    private readonly Logger _batching;

    public DatabaseLogSink(IServiceProvider services, IOptions<AppLoggingSettings> options, IHostEnvironment environment)
        : this(new AppLogBatchWriter(services, environment.ApplicationName), options.Value)
    {
    }

    internal DatabaseLogSink(IBatchedLogEventSink writer, AppLoggingSettings settings)
    {
        _minimumLevel = Enum.TryParse<LogEventLevel>(settings.MinimumLevel, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Warning;

        // A private pipeline used only for Serilog's batching (bounded queue, retries, flush on
        // dispose). Everything reaching it has already passed the filter below.
        _batching = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(writer, new BatchingOptions
            {
                BatchSizeLimit = 100,
                BufferingTimeLimit = TimeSpan.FromMilliseconds(settings.FlushIntervalMs),
                QueueLimit = settings.QueueLimit
            })
            .CreateLogger();
    }

    public void Emit(LogEvent logEvent)
    {
        if (ShouldPersist(logEvent, _minimumLevel))
        {
            _batching.Write(logEvent);
        }
    }

    internal static bool ShouldPersist(LogEvent logEvent, LogEventLevel minimumLevel)
    {
        if (logEvent.Level < minimumLevel) return false;

        // Anything logged while a batch is being written stays out, so a failing write cannot
        // feed itself.
        if (LogWriteScope.IsActive) return false;

        return !(logEvent.Properties.TryGetValue("SourceContext", out var source)
                 && source is ScalarValue { Value: string name }
                 && ExcludedSources.Contains(name));
    }

    public void Dispose() => _batching.Dispose();
}
