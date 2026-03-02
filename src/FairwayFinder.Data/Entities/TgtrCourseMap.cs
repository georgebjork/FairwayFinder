using System;

namespace FairwayFinder.Data.Entities;

public partial class TgtrCourseMap
{
    public long TgtrCourseMapId { get; set; }

    public int TgtrCourseId { get; set; }

    public long CourseId { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
