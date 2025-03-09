using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.ResponseModels;

public class ScorecardResponseModel
{
    // Course Related Data
    public Course Course { get; set; } = new();
    public Teebox Teebox { get; set; } = new();
    public List<Hole> HolesList { get; set; } = [];

    // Round related Data
    public Round Round { get; set; } = new();
    public RoundStats RoundStats { get; set; } = new();
    public List<HoleScoreQueryModel> HoleScoresList { get; set; } = [];
    public List<HoleStatsQueryModel> HoleStatsList { get; set; } = [];
    
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
}