using FairwayFinder.Core.Features.Dashboard.Models.QueryModels;
using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class DashboardRoundsTableViewModel
{
    public List<RoundsQueryModel> Rounds { get; set; } = [];
    public string Username { get; set; } = "";
}