using System;
using System.Collections.Generic;

namespace FairwayFinder.Data.Entities;

public partial class Teebox
{
    public long TeeboxId { get; set; }

    public long CourseId { get; set; }

    public string TeeboxName { get; set; } = null!;

    public long Par { get; set; }

    public decimal Rating { get; set; }

    public long Slope { get; set; }

    public long YardageOut { get; set; }

    public long YardageIn { get; set; }

    public long YardageTotal { get; set; }

    public bool IsNineHole { get; set; }

    public bool IsWomens { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
