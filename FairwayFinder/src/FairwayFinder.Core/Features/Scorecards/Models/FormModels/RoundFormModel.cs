using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class RoundFormModel
{
    public long? RoundId { get; set; }
    
    [Required]
    public long CourseId {get; set;}

    [Display(Name = "Course Name")]
    public string CourseName { get; set; } = "";

    [Required] 
    public long TeeboxId { get; set; }

    [Required]
    [Display(Name = "Date Played")]
    public DateTime DatePlayed { get; set; } = DateTime.UtcNow;

    [Required] public bool UsingHoleStats { get; set; } = true;
}