using FairwayFinder.Core.Stats;
using FairwayFinder.Core.Stats.Models.QueryModels;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class DashboardViewModel
{
    
    public List<RoundsQueryModel> Rounds { get; set; } = [];
    public RoundScoresSummaryResponse CardData { get; set; } = new();
    
    public string Username { get; set; } = "";
    
    public Dictionary<long, string> YearFilters { get; set; } = new();
    public StatsRequest Filters { get; set; } = new();
}