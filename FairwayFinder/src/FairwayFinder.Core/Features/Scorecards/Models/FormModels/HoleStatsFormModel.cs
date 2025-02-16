using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class HoleStatsFormModel
{
    public long? HoleStatsId { get; set; }
    
    [Required]
    public long HoleId { get; set; }
    
    [Required]
    public long ScoreId { get; set; }
    
    public long RoundId { get; set; }
    
    [Display(Name = "Missed Fairway")]
    public bool MissedFairway { get; set; }
    
    [Display(Name = "Miss Type")]
    public int? MissFairwayType { get; set; }
    
    [Display(Name = "Missed Green")]
    public bool MissedGreen { get; set; }
    
    [Display(Name = "Missed Type")]
    public int? MissGreenType { get; set; }
    
    [Range(0, 10)]
    [Display(Name = "Number of Putts")]
    public short? NumberOfPutts { get; set; }
    
    [Display(Name = "Approach Yardage")]
    public int? YardageOut { get; set; }
    
}
