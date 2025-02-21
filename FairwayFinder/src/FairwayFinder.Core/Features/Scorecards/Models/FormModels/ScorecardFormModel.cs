using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class ScorecardFormModel
{
    public RoundFormModel RoundFormModel { get; set; } = new();
    public List<HoleScoreFormModel> HoleScore { get; set; } = [];

    
    // Non form fields
    public Course Course { get; set; } = new();
    public Teebox Teebox { get; set; } = new();
    public Dictionary<long, string> TeeboxSelectList { get; set; } = new();
    public Dictionary<int, string> MissTypeSelectList { get; set; } = new();

    public bool IsUpdate { get; set; }
}