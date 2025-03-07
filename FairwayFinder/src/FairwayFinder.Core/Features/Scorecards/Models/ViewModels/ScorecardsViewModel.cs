using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.ViewModels;

public class ScorecardsViewModel
{
    public List<RoundSummaryQueryModel> Rounds { get; set; } = [];
    public string Username { get; set; } = "";
}