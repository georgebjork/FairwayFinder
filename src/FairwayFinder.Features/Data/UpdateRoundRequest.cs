namespace FairwayFinder.Features.Data;

/// <summary>
/// Request DTO for updating an existing round.
/// Course and round type (18/9) are locked — only teebox, date, scores, and stats can change.
/// </summary>
public class UpdateRoundRequest
{
    public long RoundId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long TeeboxId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public bool UsingHoleStats { get; set; }
    public bool UsingShotTracking { get; set; }
    public List<HoleScoreEntry> Holes { get; set; } = new();
}
