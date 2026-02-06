using System;
using System.Collections.Generic;

namespace FairwayFinder.Data.Entities;

public partial class Score
{
    public long ScoreId { get; set; }

    public long RoundId { get; set; }

    public long HoleId { get; set; }

    public short HoleScore { get; set; }

    public string UserId { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
