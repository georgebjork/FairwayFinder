namespace FairwayFinder.Data.Entities;

public partial class Hole
{
    public long HoleId { get; set; }

    public long TeeboxId { get; set; }

    public long CourseId { get; set; }

    public int HoleNumber { get; set; }

    public int Yardage { get; set; }

    public int Handicap { get; set; }

    public int Par { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Teebox Teebox { get; set; } = null!;
    public virtual Course Course { get; set; } = null!;
}
