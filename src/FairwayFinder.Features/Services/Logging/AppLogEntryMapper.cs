using System.Text;
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;

namespace FairwayFinder.Features.Services.Logging;

/// <summary>Turns a Serilog <see cref="LogEvent"/> into an <see cref="AppLogEntry"/> row.</summary>
public static class AppLogEntryMapper
{
    // Lifted into their own columns, so left out of the properties JSON.
    private static readonly HashSet<string> MappedProperties =
        ["SourceContext", "UserId", "UserEmail", "RequestPath", "Application"];

    private static readonly JsonValueFormatter JsonFormatter = new(typeTagName: null);

    // :l renders string values bare. RenderMessage() would quote them ("Round \"5\" failed"),
    // which reads as noise on the admin page.
    private static readonly MessageTemplateTextFormatter MessageFormatter = new("{Message:l}");

    public static AppLogEntry From(LogEvent logEvent, string application) => new()
    {
        Timestamp = logEvent.Timestamp.UtcDateTime,
        Level = logEvent.Level.ToString(),
        Message = RenderMessage(logEvent),
        MessageTemplate = logEvent.MessageTemplate.Text,
        Exception = logEvent.Exception?.ToString(),
        SourceContext = Truncate(GetString(logEvent, "SourceContext"), AppLogColumns.SourceContext),
        Application = Truncate(application, AppLogColumns.Application)!,
        UserId = Truncate(GetString(logEvent, "UserId"), AppLogColumns.UserId),
        UserEmail = Truncate(GetString(logEvent, "UserEmail"), AppLogColumns.UserEmail),
        RequestPath = Truncate(GetString(logEvent, "RequestPath"), AppLogColumns.RequestPath),
        TraceId = Truncate(logEvent.TraceId?.ToHexString(), AppLogColumns.TraceId),
        Properties = SerializeProperties(logEvent)
    };

    private static string RenderMessage(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        MessageFormatter.Format(logEvent, writer);
        return writer.ToString();
    }

    private static string? GetString(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: { } raw }
            ? raw.ToString()
            : null;

    private static string? SerializeProperties(LogEvent logEvent)
    {
        var remaining = logEvent.Properties.Where(p => !MappedProperties.Contains(p.Key)).ToList();
        if (remaining.Count == 0) return null;

        var json = new StringBuilder();
        using var writer = new StringWriter(json);
        writer.Write('{');
        for (var i = 0; i < remaining.Count; i++)
        {
            if (i > 0) writer.Write(',');
            JsonValueFormatter.WriteQuotedJsonString(remaining[i].Key, writer);
            writer.Write(':');
            JsonFormatter.Format(remaining[i].Value, writer);
        }
        writer.Write('}');

        return json.ToString();
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
