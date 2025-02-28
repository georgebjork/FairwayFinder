using FairwayFinder.Core.Features.Stats;

namespace FairwayFinder.Core.Features.Dashboard.Models.ViewModel;

public class DashboardViewModel
{
    public Dictionary<long, string> YearFilters { get; set; } = new();
    public StatsRequest Filters { get; set; } = new();
}