using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

public partial class TgtrTeeboxMap : IAuditable
{
    public long TgtrTeeboxMapId { get; set; }

    public int TgtrTeeboxId { get; set; }

    public long TeeboxId { get; set; }

    public int TgtrCourseId { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Teebox Teebox { get; set; } = null!;
}
