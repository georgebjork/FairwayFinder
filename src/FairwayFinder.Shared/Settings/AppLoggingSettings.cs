namespace FairwayFinder.Shared.Settings;

/// <summary>Persisting Warning-or-worse log events to the logs database for the admin console.</summary>
public class AppLoggingSettings
{
    /// <summary>Master switch for the database log sink.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Lowest Serilog level written to the database (Verbose, Debug, Information, Warning, Error, Fatal).
    /// </summary>
    public string MinimumLevel { get; set; } = "Warning";

    /// <summary>Entries older than this many days are removed by the purge.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>Events buffered while the database is slow or down; beyond this, new events are dropped.</summary>
    public int QueueLimit { get; set; } = 10000;

    /// <summary>How long events are buffered before a batch is written.</summary>
    public int FlushIntervalMs { get; set; } = 2000;
}
