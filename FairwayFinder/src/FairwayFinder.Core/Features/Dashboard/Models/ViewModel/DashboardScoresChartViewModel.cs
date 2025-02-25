using FairwayFinder.Core.Features.Stats.Models.QueryModels;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class DashboardScoresChartViewModel
{
    public List<RoundScoreQueryModel> Scores { get; set; } = [];
}