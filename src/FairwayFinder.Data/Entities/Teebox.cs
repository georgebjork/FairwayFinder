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

    /// <summary>
    /// Lineage key linking all versions of the same logical tee. New teeboxes get their own id;
    /// a teebox created via "save as new version" inherits the source's group id so stats can
    /// treat the whole lineage as one tee while each round keeps its own historical values.
    /// </summary>
    public long TeeboxGroupId { get; set; }

    /// <summary>Null = active. Set when this teebox has been superseded by a newer version.</summary>
    public DateOnly? ArchivedOn { get; set; }

    /// <summary>UserId of the admin who created the superseding version. Null while active.</summary>
    public string? ArchivedBy { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Course Course { get; set; } = null!;
}
