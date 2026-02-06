using System;
using System.Collections.Generic;

namespace FairwayFinder.Data.Entities;

public partial class Course
{
    public long CourseId { get; set; }

    public string CourseName { get; set; } = null!;

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
