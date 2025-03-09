using FairwayFinder.Core.Models;
using FairwayFinder.Core.Stats.Models.QueryModels;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class DashboardScoresChartViewModel
{
    public List<RoundsQueryModel> Rounds { get; set; } = [];
}