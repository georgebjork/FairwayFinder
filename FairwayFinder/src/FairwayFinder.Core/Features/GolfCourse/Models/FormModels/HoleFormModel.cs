using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.GolfCourse.Models.FormModels;

public class HoleFormModel
{
    public long? HoleId { get; set; }
    public long? TeeboxId { get; set; }
    public long? CourseId { get; set; }
    
    [Required]
    [Display(Name = "Hole")]
    [Range(1, 18)]
    public long HoleNumber { get; set; }
    
    [Required]
    [Display(Name = "Yardage")]
    [Range(1, 1000)]
    public long Yardage { get; set; }
    
    [Required]
    [Display(Name = "Handicap")]
    [Range(1, 18)]
    public long Handicap { get; set; }
    
    [Required]
    [Display(Name = "Par")]
    [Range(3, 7)]
    public long Par { get; set; }
    
    public bool ParHandicapReadonly { get; set; }
}