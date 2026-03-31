namespace FairwayFinder.Data.Entities;

public partial class Shot
{
    public long ShotId { get; set; }
    public long ScoreId { get; set; }

    public int ShotNumber { get; set; }

    public int StartDistance { get; set; }
    public string StartDistanceUnit { get; set; } = null!;
    public int StartLie { get; set; }

    public int? EndDistance { get; set; }
    public string? EndDistanceUnit { get; set; }
    public int? EndLie { get; set; }

    public int PenaltyStrokes { get; set; }

    public string CreatedBy { get; set; } = null!;
    public DateOnly CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = null!;
    public DateOnly UpdatedOn { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Score Score { get; set; } = null!;
}
