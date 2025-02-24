namespace FairwayFinder.Core.Features.Stats.Models.QueryModels;

public class RoundScoreStats
{
    public long hole_in_one { get; set; }
    public long double_eagles { get; set; }
    public long eagles { get; set; }
    public long birdies { get; set; }
    public long pars { get; set; }
    public long bogies { get; set; }
    public long double_bogies { get; set; }
    public long triple_or_worse { get; set; }
}