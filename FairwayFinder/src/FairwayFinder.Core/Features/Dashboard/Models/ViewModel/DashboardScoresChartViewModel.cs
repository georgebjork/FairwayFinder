using FairwayFinder.Core.Features.Dashboard.Models.QueryModels;
using FairwayFinder.Core.Features.Stats.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class DashboardScoresChartViewModel
{
    public List<RoundsQueryModel> Rounds { get; set; } = [];
}