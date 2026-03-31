namespace FairwayFinder.Features.Data;

/// <summary>
/// Strokes gained summary for a round or aggregated across multiple rounds.
/// All values are totals (for a single round) or per-round averages (for aggregates).
/// </summary>
public class StrokesGainedSummary
{
    public double SgTotal { get; set; }
    public double SgPutting { get; set; }
    public double SgTeeToGreen { get; set; }
    public double SgOffTheTee { get; set; }
    public double SgApproach { get; set; }
    public double SgAroundTheGreen { get; set; }

    public int RoundsIncluded { get; set; }
    public int HolesWithShots { get; set; }

    // Trends (set on aggregate summaries, null on single-round)
    public double? SgTotalTrend { get; set; }
    public double? SgPuttingTrend { get; set; }
    public double? SgTeeToGreenTrend { get; set; }
    public double? SgOffTheTeeTrend { get; set; }
    public double? SgApproachTrend { get; set; }
    public double? SgAroundTheGreenTrend { get; set; }
}
