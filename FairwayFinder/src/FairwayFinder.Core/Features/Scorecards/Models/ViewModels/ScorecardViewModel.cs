using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Features.Scorecards.Models.ResponseModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.ViewModels;

public class ScorecardViewModel
{
    public ScorecardResponseModel Scorecard { get; set; } = new();
    public ScorecardRoundStats ScorecardRoundStats { get; set; } = new();
    
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";


    
    

}