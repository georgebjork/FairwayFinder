using FairwayFinder.Core.Stats.Models.QueryModels;

namespace FairwayFinder.Core.Features.Scorecards.Models;

public class ScorecardRoundStats
{
    public RoundScoreStatsQueryModel ScoreCountStatsQueryModel { get; set; } = new();
    public List<AverageScoreByParQueryModel> AverageScoreByParQueryModel { get; set; } = new();
    
    public int Par3ScoreToPar { get; set; }
    public double Par3AverageScoreToPar { get; set; }

    
    public int Par4ScoreToPar { get; set; }
    public double Par4AverageScoreToPar { get; set; }

    
    public int Par5ScoreToPar { get; set; }
    public double Par5AverageScoreToPar { get; set; }

}