using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class DashboardRoundsTableViewModel
{
    public List<RoundSummaryQueryModel> Rounds { get; set; } = [];
    public string Username { get; set; } = "";
}