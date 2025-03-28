namespace FairwayFinder.Core.Features.Dashboard.Models;

public class RoundScoresSummaryResponse
{
    public long RoundsPlayed { get; set; }
    public double AvgScore { get; set; }
    public int LowRound { get; set; }
    public double AvgScoreNineHole { get; set; }
    public int LowRoundNineHole { get; set; }
}