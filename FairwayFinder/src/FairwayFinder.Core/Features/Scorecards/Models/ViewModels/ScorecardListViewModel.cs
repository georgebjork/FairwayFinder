using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.ViewModels;

public class ScorecardListViewModel
{
    public List<ScorecardSummaryQueryModel> Scorecards { get; set; } = [];
    public string Username { get; set; } = "";
}