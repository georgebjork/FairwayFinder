using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.FormModels;

public class RoundFormModel
{
    [Required]
    public long CourseId {get; set;}

    [Display(Name = "Course Name")]
    public string CourseName { get; set; } = "";
    
    
    public Course Course {get; set;} = new Course();
}