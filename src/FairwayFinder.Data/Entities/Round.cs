using System;
using System.Collections.Generic;

namespace FairwayFinder.Data.Entities;

public partial class Round
{
    public long RoundId { get; set; }

    public long CourseId { get; set; }

    public long TeeboxId { get; set; }

    public DateOnly DatePlayed { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    public int Score { get; set; }

    public int ScoreOut { get; set; }

    public int ScoreIn { get; set; }

    public string UserId { get; set; } = null!;

    public bool UsingHoleStats { get; set; }

    public bool ExcludeFromStats { get; set; }

    public bool FullRound { get; set; }

    public bool FrontNine { get; set; }

    public bool BackNine { get; set; }
}
