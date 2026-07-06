namespace FairwayFinder.Features.Services.Admin;

/// <summary>A page of results plus the total row count, for server-side paged grids.</summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class ApiRequestLogListItemDto
{
    public long ApiRequestLogId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? RouteTemplate { get; set; }
    public string? QueryString { get; set; }
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? TraceId { get; set; }
}
