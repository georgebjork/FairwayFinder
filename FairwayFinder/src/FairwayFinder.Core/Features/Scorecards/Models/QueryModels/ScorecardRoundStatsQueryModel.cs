namespace FairwayFinder.Core.Features.Scorecards.Models.QueryModels;

public class ScorecardRoundStatsQueryModel
{
    public int hole_in_ones { get; set; }
    public int double_eagles { get; set; }
    public int eagles { get; set; }
    public int birdies { get; set; }
    public int pars { get; set; }
    public int bogies { get; set; }
    public int double_bogies { get; set; }
    public int triple_or_worse { get; set; }
}