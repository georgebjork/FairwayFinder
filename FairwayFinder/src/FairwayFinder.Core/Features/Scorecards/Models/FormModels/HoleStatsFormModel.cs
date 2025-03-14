﻿using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Validation;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class HoleStatsFormModel
{
    public long? HoleStatsId { get; set; }
    
    [Required]
    public long HoleId { get; set; }
    
    [Required]
    public long ScoreId { get; set; }
    
    public long RoundId { get; set; }
    public bool MissedFairway { get; set; }
    public bool HitFairway { get; set; }
    public bool TeeShotOb { get; set; }

    
    [RequiredIf(nameof(MissedFairway), true)]
    [Display(Name = "Miss Type")]
    public int? MissFairwayType { get; set; }
    
   
    public bool HitGreen { get; set; }
    public bool MissedGreen { get; set; }
    public bool ApproachShotOb { get; set; }
    
    [RequiredIf(nameof(MissedGreen), true)]
    [Display(Name = "Missed Type")]
    public int? MissGreenType { get; set; }
    
    [Range(0, 10)]
    [Display(Name = "Putts")]
    public short? NumberOfPutts { get; set; }
    
    [Display(Name = "APY")]
    public int? YardageOut { get; set; }
    
}