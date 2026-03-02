namespace FairwayFinder.Data.Entities;

public partial class Teebox
{
    public long TeeboxId { get; set; }

    public long CourseId { get; set; }

    public string TeeboxName { get; set; } = null!;

    public int Par { get; set; }

    public decimal Rating { get; set; }

    public int Slope { get; set; }

    public int YardageOut { get; set; }

    public int YardageIn { get; set; }

    public int YardageTotal { get; set; }

    public bool IsNineHole { get; set; }

    public bool IsWomens { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Course Course { get; set; } = null!;
}
