using System.Collections.Concurrent;
using FairwayFinder.Features.Services.Logging;
using FairwayFinder.Shared.Settings;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace FairwayFinder.Features.Tests.Logging;

public class DatabaseLogSinkTests
{
    [Theory]
    [InlineData(LogEventLevel.Debug, false)]
    [InlineData(LogEventLevel.Information, false)]
    [InlineData(LogEventLevel.Warning, true)]
    [InlineData(LogEventLevel.Error, true)]
    [InlineData(LogEventLevel.Fatal, true)]
    public void Only_warning_and_worse_are_persisted(LogEventLevel level, bool expected)
    {
        Assert.Equal(expected, DatabaseLogSink.ShouldPersist(Event(level), LogEventLevel.Warning));
    }

    [Fact]
    public void The_request_logging_summary_line_is_not_persisted()
    {
        var logEvent = Event(LogEventLevel.Error, ("SourceContext", "Serilog.AspNetCore.RequestLoggingMiddleware"));

        Assert.False(DatabaseLogSink.ShouldPersist(logEvent, LogEventLevel.Warning));
    }

    [Fact]
    public async Task Events_logged_while_a_batch_is_being_written_are_not_fed_back_in()
    {
        var writer = new ReentrantWriter();
        using (var sink = new DatabaseLogSink(writer, new AppLoggingSettings { FlushIntervalMs = 10 }))
        {
            writer.Sink = sink;
            sink.Emit(Event(LogEventLevel.Error));
            await writer.FirstBatch.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        // Disposing flushed the pipeline: had the event logged mid-write got in, there'd be a second.
        Assert.Single(writer.Received);
    }

    [Fact]
    public void Mapper_lifts_user_request_and_source_into_columns_and_keeps_the_rest_as_json()
    {
        var logEvent = Event(LogEventLevel.Error,
            ("SourceContext", "FairwayFinder.Features.Services.RoundService"),
            ("UserId", "u-1"),
            ("UserEmail", "golfer@test.com"),
            ("RequestPath", "/api/rounds/5"),
            ("RoundId", 5L));

        var entry = AppLogEntryMapper.From(logEvent, "FairwayFinder.Api");

        Assert.Equal("Error", entry.Level);
        Assert.Equal("Round 5 failed", entry.Message);
        Assert.Equal("Round {RoundId} failed", entry.MessageTemplate);
        Assert.Equal("FairwayFinder.Features.Services.RoundService", entry.SourceContext);
        Assert.Equal("u-1", entry.UserId);
        Assert.Equal("golfer@test.com", entry.UserEmail);
        Assert.Equal("/api/rounds/5", entry.RequestPath);
        Assert.Equal("FairwayFinder.Api", entry.Application);
        Assert.Equal("{\"RoundId\":5}", entry.Properties);
        Assert.Contains("InvalidOperationException: boom", entry.Exception);
    }

    [Fact]
    public void Mapper_renders_string_values_without_quotes_and_truncates_to_the_column()
    {
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Warning, null,
            new MessageTemplateParser().Parse("Course {Name} skipped"),
            [
                new LogEventProperty("Name", new ScalarValue("Pebble Beach")),
                new LogEventProperty("UserEmail", new ScalarValue(new string('a', 400)))
            ]);

        var entry = AppLogEntryMapper.From(logEvent, "FairwayFinder.Admin");

        Assert.Equal("Course Pebble Beach skipped", entry.Message);
        Assert.Equal(256, entry.UserEmail!.Length);
        Assert.Null(entry.Exception);
    }

    private static LogEvent Event(LogEventLevel level, params (string Name, object Value)[] properties) => new(
        DateTimeOffset.UtcNow,
        level,
        new InvalidOperationException("boom"),
        new MessageTemplateParser().Parse("Round {RoundId} failed"),
        properties.Select(p => new LogEventProperty(p.Name, new ScalarValue(p.Value)))
            .Append(new LogEventProperty("RoundId", new ScalarValue(5L)))
            .DistinctBy(p => p.Name));

    /// <summary>Logs back into the sink from inside its own write, as a failing EF save would.</summary>
    private sealed class ReentrantWriter : IBatchedLogEventSink
    {
        public DatabaseLogSink? Sink { get; set; }
        public ConcurrentBag<LogEvent> Received { get; } = [];
        public TaskCompletionSource FirstBatch { get; } = new();

        public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
        {
            // The same scope the real writer runs its EF save in.
            await LogWriteScope.RunAsync(() =>
            {
                foreach (var e in batch) Received.Add(e);
                Sink!.Emit(Event(LogEventLevel.Error));
                return Task.CompletedTask;
            });
            FirstBatch.TrySetResult();
        }
    }
}
