using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

public partial class TgtrCourseMap : IAuditable
{
    public long TgtrCourseMapId { get; set; }

    public int TgtrCourseId { get; set; }

    public long CourseId { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Course Course { get; set; } = null!;
}
