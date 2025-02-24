using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class RoundStatsViewModel
{
    public List<RoundStats> RoundStatsList { get; set; } = new();
}