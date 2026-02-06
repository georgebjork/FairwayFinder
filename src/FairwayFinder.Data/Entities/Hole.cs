using System;
using System.Collections.Generic;

namespace FairwayFinder.Data.Entities;

public partial class Hole
{
    public long HoleId { get; set; }

    public long TeeboxId { get; set; }

    public long CourseId { get; set; }

    public long HoleNumber { get; set; }

    public long Yardage { get; set; }

    public long Handicap { get; set; }

    public long Par { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
