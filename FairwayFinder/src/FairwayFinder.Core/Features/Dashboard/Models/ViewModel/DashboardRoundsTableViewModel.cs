using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Stats.Models.QueryModels;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class DashboardRoundsTableViewModel
{
    public List<RoundsQueryModel> Rounds { get; set; } = [];
    public string Username { get; set; } = "";
}