using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class CreateRoundFormModel
{
    [Required]
    public long? CourseId {get; set;}

    [Display(Name = "Course Name")]
    public string CourseName { get; set; } = "";

    [Required] 
    public string? TeeboxId { get; set; } = ""; // This is intentional for the dropdown list
    
    public List<HoleScoreFormModel> HoleScore { get; set; } = [];
    
    
    // Non form fields
    public Course Course { get; set; } = new();
    public Teebox Teebox { get; set; } = new();
    public Dictionary<string, string> TeeboxSelectList { get; set; } = new();
}