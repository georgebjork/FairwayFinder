namespace FairwayFinder.Features.Data;

/// <summary>
/// Per-hole stats for a user at a particular course. One <see cref="HoleStatsDetail"/>
/// entry per hole the user has actually played under the active filters — holes the user
/// has never played at this course (or that are filtered out) are not included.
///
/// Teebox options + the active teebox selection are returned inline so the iOS hole-detail
/// screen can drive its own filter sheet without a second fetch.
/// </summary>
public class CourseHoleStatsResponse
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Number of rounds in the filtered set.</summary>
    public int TotalRounds { get; set; }

    /// <summary>One entry per played hole number, ordered by HoleNumber.</summary>
    public List<HoleStatsDetail> Holes { get; set; } = new();

    /// <summary>Available teeboxes the user has played at this course (for filter dropdown).</summary>
    public List<CourseTeeboxOption> TeeboxOptions { get; set; } = new();

    /// <summary>The teebox ID currently being filtered on. Null = all teeboxes.</summary>
    public long? SelectedTeeboxId { get; set; }
}

/// <summary>
/// Everything the iOS HoleDetailView needs for one hole. Inherits from
/// <see cref="HoleAggregateStats"/> so all aggregate fields (HoleNumber, Par,
/// AverageScore, FairwayHitPercent, …) serialize at the top level, with the
/// new per-hole-detail fields (scoring distribution, plays list, averaged SG,
/// trend slopes) alongside them.
/// </summary>
public class HoleStatsDetail : HoleAggregateStats
{
    /// <summary>
    /// Scoring buckets for this hole only (score==1 → hole-in-one regardless of par;
    /// otherwise bucket by score − par). <c>RoundsIncluded</c> is set to the play count.
    /// </summary>
    public ScoringDistribution ScoringDistribution { get; set; } = new();

    /// <summary>
    /// Every play of this hole in the filtered set, newest first. Drives the iOS
    /// scoring-bucket list and round-deep-link navigation. Each play also carries
    /// per-shot stats (FIR / GIR / putts / SG) when available, so the iOS side can
    /// build the per-hole trend mini-charts without a follow-up request.
    /// </summary>
    public List<HolePlay> Plays { get; set; } = new();

    /// <summary>
    /// Averaged strokes gained for this hole. Null when no filtered round has
    /// shot tracking on this hole.
    /// </summary>
    public HoleAverageSg? StrokesGained { get; set; }

    /// <summary>
    /// Per-play linear-regression slope of the score on this hole. Lower is better.
    /// Null when fewer than two scored plays. The iOS hole-detail screen uses this
    /// for the Avg Score card's trend arrow and the Score Trend drill-down chart.
    /// Fairway / GIR / putts / SG trends were deliberately removed — only the score
    /// trend proved useful at hole granularity.
    /// </summary>
    public double? AverageScoreTrend { get; set; }
}

/// <summary>
/// A single play of a hole in the filtered set. <see cref="ScoreToPar"/> is
/// precomputed (Score − Par); the iOS client uses it to bucket plays under the
/// matching slice of the scoring distribution and to tap-through to the round.
/// </summary>
public class HolePlay
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public short Score { get; set; }
    public int ScoreToPar { get; set; }

    /// <summary>
    /// The teebox the round was played from. Lets the iOS play list surface
    /// "Blue" vs "White" alongside the date so the user can tell which tees
    /// produced which score without opening the round.
    /// </summary>
    public string TeeboxName { get; set; } = string.Empty;
}

/// <summary>
/// Averaged strokes gained for a single hole across the filtered, shot-tracked rounds.
/// Values are rounded to 2 dp, matching <c>AggregateStoredSg</c> in <c>StatsService</c>.
/// </summary>
public class HoleAverageSg
{
    /// <summary>Number of shot-tracked plays of this hole averaged.</summary>
    public int Count { get; set; }
    public double SgTotal { get; set; }
    public double SgOffTheTee { get; set; }
    public double SgApproach { get; set; }
    public double SgAroundTheGreen { get; set; }
    public double SgPutting { get; set; }
}
