using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class HoleStatsFormModel
{
    [Required]
    public int HoleId { get; set; }
    public int RoundId { get; set; }
    
    [Display(Name = "Missed Fairway")]
    public bool MissedFairway { get; set; }
    [Display(Name = "Miss Type")]
    public int? MissFairwayType { get; set; }
    
    [Display(Name = "Missed Green")]
    public bool MissedGreen { get; set; }
    
    [Display(Name = "Missed Type")]
    public int? MissGreenType { get; set; }
    
    [Display(Name = "Number of Putts")]
    public int NumberOfPutts { get; set; }
    
    [Display(Name = "Approach Yardage")]
    public int YardageOut { get; set; }
    
}