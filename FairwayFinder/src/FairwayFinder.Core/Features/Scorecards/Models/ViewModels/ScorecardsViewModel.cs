using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.ViewModels;

public class ScorecardsViewModel
{
    public List<RoundsQueryModel> Rounds { get; set; } = [];
}