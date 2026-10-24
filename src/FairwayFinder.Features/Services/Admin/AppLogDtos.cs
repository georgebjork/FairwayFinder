namespace FairwayFinder.Features.Services.Admin;

/// <summary>A row in the error log grid. Omits the stack trace and properties, which can be large.</summary>
public class AppLogListItemDto
{
    public long AppLogId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SourceContext { get; set; }
    public string Application { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public bool HasException { get; set; }
}

/// <summary>Everything stored for one log event, loaded when its row is expanded.</summary>
public class AppLogDetailDto : AppLogListItemDto
{
    public string MessageTemplate { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? RequestPath { get; set; }
    public string? TraceId { get; set; }
    public string? Properties { get; set; }
}
