﻿using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class HoleScoreFormModel
{
    public long? ScoreId { get; set; }
    
    [Required]
    public long HoleId { get; set; } 
    
    [Required]
    public long HoleNumber { get; set; }
    
    [Required]
    [Range(1, 20)]
    public short Score { get; set; }
    
    
    // Non form fields
    public long Par { get; set; }
    public long Yardage { get; set; } 
}