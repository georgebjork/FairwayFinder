namespace FairwayFinder.Data.Entities;

/// <summary>
/// Immutable record of a single Warning-or-worse log event from either host. Written by the
/// Serilog database sink and hard-deleted by the retention purge — no audit/soft-delete fields.
/// </summary>
public class AppLogEntry
{
    public long AppLogId { get; set; }

    /// <summary>UTC timestamp of when the event was logged.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Serilog level name: Warning, Error or Fatal.</summary>
    public string Level { get; set; } = null!;

    /// <summary>The message with its property values substituted.</summary>
    public string Message { get; set; } = null!;

    /// <summary>The raw message template, identical across every occurrence of the same event.</summary>
    public string MessageTemplate { get; set; } = null!;

    /// <summary>The exception's full <c>ToString()</c> — type, message, stack trace and inner exceptions.</summary>
    public string? Exception { get; set; }

    /// <summary>Logger category, usually the full name of the class that logged.</summary>
    public string? SourceContext { get; set; }

    /// <summary>The host that logged it (FairwayFinder.Api or FairwayFinder.Admin).</summary>
    public string Application { get; set; } = null!;

    /// <summary>The signed-in user the event happened under; null for anonymous requests and background jobs.</summary>
    public string? UserId { get; set; }

    /// <summary>Denormalized email for display without a cross-database join.</summary>
    public string? UserEmail { get; set; }

    public string? RequestPath { get; set; }

    public string? TraceId { get; set; }

    /// <summary>Every other event property as a JSON object.</summary>
    public string? Properties { get; set; }
}
