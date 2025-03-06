using FairwayFinder.Core.Stats.Models.QueryModels;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class RoundStatsViewModel
{
    public RoundScoreStatsQueryModel ScoreStatsQueryModel { get; set; } = new();
}