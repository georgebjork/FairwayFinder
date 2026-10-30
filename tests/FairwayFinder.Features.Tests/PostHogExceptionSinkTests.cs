using FairwayFinder.Features.Services.Analytics;
using PostHog;
using Serilog.Events;
using Serilog.Parsing;

namespace FairwayFinder.Features.Tests;

public class PostHogExceptionSinkTests
{
    private static LogEvent Event(
        LogEventLevel level,
        Exception? exception,
        params (string Name, string Value)[] properties)
        => new(
            DateTimeOffset.UtcNow,
            level,
            exception,
            new MessageTemplateParser().Parse("Round {RoundId} failed"),
            properties.Select(p => new LogEventProperty(p.Name, new ScalarValue(p.Value))));

    [Theory]
    [InlineData(LogEventLevel.Error, true)]
    [InlineData(LogEventLevel.Fatal, true)]
    [InlineData(LogEventLevel.Warning, false)]
    [InlineData(LogEventLevel.Information, false)]
    public void Only_error_or_worse_is_captured(LogEventLevel level, bool expected)
    {
        Assert.Equal(expected, PostHogExceptionSink.ShouldCapture(Event(level, new InvalidOperationException())));
    }

    [Fact]
    public void An_error_without_an_exception_is_not_captured()
    {
        Assert.False(PostHogExceptionSink.ShouldCapture(Event(LogEventLevel.Error, exception: null)));
    }

    [Fact]
    public void The_sdks_own_errors_are_not_captured()
    {
        var fromSdk = Event(LogEventLevel.Error, new HttpRequestException(), ("SourceContext", "PostHog.Api.PostHogApiClient"));

        Assert.False(PostHogExceptionSink.ShouldCapture(fromSdk));
    }

    [Fact]
    public void Request_context_and_the_template_travel_with_the_exception()
    {
        var logEvent = Event(LogEventLevel.Error, new InvalidOperationException(),
            ("SourceContext", "FairwayFinder.Features.Services.RoundService"),
            ("RouteTemplate", "/api/rounds/{roundId:long}"),
            ("UserId", "golfer-1"));

        var properties = PostHogExceptionSink.BuildProperties(logEvent, anonymous: false);

        Assert.Equal("Round {RoundId} failed", properties["log_message_template"]);
        Assert.Equal("FairwayFinder.Features.Services.RoundService", properties["SourceContext"]);
        Assert.Equal("/api/rounds/{roundId:long}", properties["RouteTemplate"]);
        Assert.False(properties.ContainsKey("$process_person_profile"));
    }

    [Fact]
    public void An_exception_with_no_golfer_does_not_create_a_person()
    {
        var properties = PostHogExceptionSink.BuildProperties(
            Event(LogEventLevel.Error, new InvalidOperationException()), anonymous: true);

        Assert.Equal(false, properties["$process_person_profile"]);
    }

    [Fact]
    public void A_failing_client_never_throws_back_into_the_code_that_logged()
    {
        var sink = new PostHogExceptionSink(
            new Lazy<IPostHogClient>(() => throw new InvalidOperationException("PostHog is down")),
            "FairwayFinder.Api");

        sink.Emit(Event(LogEventLevel.Error, new InvalidOperationException(), ("UserId", "golfer-1")));
    }
}
