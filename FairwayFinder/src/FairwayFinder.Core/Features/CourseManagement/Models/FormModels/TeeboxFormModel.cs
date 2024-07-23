using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.CourseManagement.Models.FormModels;

public class TeeboxFormModel
{
    public long? TeeboxId { get; set; }
    
    [Required]
    public long CourseId { get; set; }
    
    [Display(Name = "Teebox Name")]
    public string Name { get; set; } = "";
    
    [Required]
    [Display(Name = "Par")]
    public long Par { get; set; }
    
    [Required]
    [Display(Name = "Rating")]
    public decimal Rating { get; set; }
    
    [Required]
    [Display(Name = "Slope")]
    public long Slope { get; set; }
    
    [Required]
    [Display(Name = "Yardage Out")]
    public long YardageOut { get; set; }
    
    [Display(Name = "Yardage In")]
    public long? YardageIn { get; set; }
    
    public long Yardage => (YardageIn ?? 0) + YardageOut;
    
    [Display(Name = "Nine Holes")]
    public bool IsNineHoles { get; set; }
    
    [Display(Name = "Women's Tees")]
    public bool IsWomenTees { get; set; }
}