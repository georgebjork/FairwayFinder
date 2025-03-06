namespace FairwayFinder.Core.Stats;

public class StatsRequest
{
    public string UserId { get; set; } = "";
    public long? Year { get; set; } 
    public long? RoundId { get; set; }
}