namespace FairwayFinder.Features.Data;

/// <summary>
/// Request DTO for creating a new round.
/// </summary>
public class CreateRoundRequest
{
    public string UserId { get; set; } = string.Empty;
    public long CourseId { get; set; }
    public long TeeboxId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public bool FullRound { get; set; }
    public bool FrontNine { get; set; }
    public bool BackNine { get; set; }
    public bool UsingHoleStats { get; set; }
    public List<HoleScoreEntry> Holes { get; set; } = new();
}

/// <summary>
/// Per-hole score entry with optional advanced stats.
/// </summary>
public class HoleScoreEntry
{
    /// <summary>
    /// The score's primary key. 0 for new rounds; populated when editing an existing round.
    /// Used to match existing scores during updates (instead of HoleId, which changes with teebox).
    /// </summary>
    public long ScoreId { get; set; }
    public long HoleId { get; set; }
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public short Score { get; set; }
    
    // Advanced stats (only populated when UsingHoleStats is true)
    public bool? HitFairway { get; set; }
    public long? MissFairwayType { get; set; }
    public bool? HitGreen { get; set; }
    public long? MissGreenType { get; set; }
    public short? NumberOfPutts { get; set; }
}

/// <summary>
/// Course search result for the course picker dropdown.
/// </summary>
public class CourseSearchResult
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string? Address { get; set; }
}

/// <summary>
/// Teebox option for the teebox picker.
/// </summary>
public class TeeboxOption
{
    public long TeeboxId { get; set; }
    public string TeeboxName { get; set; } = string.Empty;
    public int Par { get; set; }
    public decimal Rating { get; set; }
    public int Slope { get; set; }
    public int YardageTotal { get; set; }
    public bool IsNineHole { get; set; }
    public bool IsWomens { get; set; }
    
    /// <summary>
    /// Display label for the dropdown (e.g., "Blue - 72 par, 6800 yds, 72.1/131")
    /// </summary>
    public string DisplayLabel => $"{TeeboxName} - {Par} par, {YardageTotal} yds, {Rating:F1}/{Slope}";
}

/// <summary>
/// Hole information for building the scorecard entry form.
/// </summary>
public class HoleInfo
{
    public long HoleId { get; set; }
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int Yardage { get; set; }
    public int Handicap { get; set; }
}
