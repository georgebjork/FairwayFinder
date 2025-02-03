using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class HoleScoreFormModel
{
    [Required]
    public long? HoleId { get; set; } 
    
    [Required]
    [Range(1, 20)]
    public int? Score { get; set; }

    public Hole Hole { get; set; } = new();
}