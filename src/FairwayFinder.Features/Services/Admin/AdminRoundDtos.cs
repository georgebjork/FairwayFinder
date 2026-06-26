namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Lightweight row for the admin "all rounds" grid — includes the owning player so rounds
/// across all users can be listed and filtered.
/// </summary>
public class AdminRoundListItemDto
{
    public long RoundId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerEmail { get; set; } = string.Empty;
    public DateOnly DatePlayed { get; set; }
    public int Score { get; set; }
    public int ToPar { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string TeeboxName { get; set; } = string.Empty;
    public bool ExcludeFromStats { get; set; }
    public bool UsingShotTracking { get; set; }
    public bool FullRound { get; set; }
}
