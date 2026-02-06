using System;
using System.Collections.Generic;

namespace FairwayFinder.Data.Entities;

public partial class HoleStat
{
    public long HoleStatsId { get; set; }

    public long ScoreId { get; set; }

    public long RoundId { get; set; }

    public long HoleId { get; set; }

    public bool? HitFairway { get; set; }

    public int? MissFairwayType { get; set; }

    public bool? HitGreen { get; set; }

    public int? MissGreenType { get; set; }

    public short? NumberOfPutts { get; set; }

    public int? ApproachYardage { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    public bool? TeeShotOb { get; set; }

    public bool? ApproachShotOb { get; set; }
}
