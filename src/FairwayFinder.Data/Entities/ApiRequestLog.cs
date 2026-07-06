namespace FairwayFinder.Data.Entities;

/// <summary>
/// Immutable, system-generated record of a single API request. Written by the API's request
/// logging middleware and hard-deleted by the retention purge — no audit/soft-delete fields.
/// </summary>
public class ApiRequestLog
{
    public long ApiRequestLogId { get; set; }

    /// <summary>UTC timestamp of when the request completed.</summary>
    public DateTime Timestamp { get; set; }

    public string Method { get; set; } = null!;

    public string Path { get; set; } = null!;

    /// <summary>Matched route pattern (e.g. "/api/rounds/{roundId}"); null when no endpoint matched.</summary>
    public string? RouteTemplate { get; set; }

    public string? QueryString { get; set; }

    public int StatusCode { get; set; }

    public int DurationMs { get; set; }

    /// <summary>Authenticated user id (from the JWT NameIdentifier claim); null for anonymous requests.</summary>
    public string? UserId { get; set; }

    /// <summary>Denormalized email from the JWT Email claim for display without a join.</summary>
    public string? UserEmail { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Correlation id (Activity id, else HttpContext.TraceIdentifier).</summary>
    public string? TraceId { get; set; }
}
