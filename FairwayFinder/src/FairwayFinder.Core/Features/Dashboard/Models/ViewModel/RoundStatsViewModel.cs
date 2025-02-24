using FairwayFinder.Core.Features.Stats.Models.QueryModels;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class RoundStatsViewModel
{
    public RoundScoreStats ScoreStats { get; set; } = new();
}