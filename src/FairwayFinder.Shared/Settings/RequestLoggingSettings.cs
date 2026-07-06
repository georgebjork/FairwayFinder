namespace FairwayFinder.Shared.Settings;

public class RequestLoggingSettings
{
    /// <summary>Master switch for API request logging.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Requests older than this many days are removed by the purge.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>Request path prefixes that should not be logged (health, docs, etc.).</summary>
    public string[] ExcludedPathPrefixes { get; set; } =
        ["/health", "/alive", "/openapi", "/scalar"];

    /// <summary>Bounded capacity of the in-memory write buffer; overflow entries are dropped.</summary>
    public int ChannelCapacity { get; set; } = 10000;
}
