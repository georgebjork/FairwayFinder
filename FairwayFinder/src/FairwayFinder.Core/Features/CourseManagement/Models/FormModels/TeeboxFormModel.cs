using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.CourseManagement.Models.FormModels;

public class TeeboxFormModel
{
    public int? TeeboxId { get; set; }
    
    [Required]
    public int CourseId { get; set; }
    
    [Display(Name = "Teebox Name")]
    public string Name { get; set; } = "";
    
    [Required]
    [Display(Name = "Par")]
    public int? Par { get; set; }
    
    [Required]
    [Display(Name = "Rating")]
    public decimal? Rating { get; set; }
    
    [Required]
    [Display(Name = "Slope")]
    public int? Slope { get; set; }
    
    [Required]
    [Display(Name = "Yardage Out")]
    public int? YardageOut { get; set; }
    
    [Display(Name = "Yardage In")]
    public int? YardageIn { get; set; }
    
    public int Yardage => YardageIn ?? 0 + YardageOut ?? 0;
    
    [Display(Name = "Nine Holes")]
    public bool IsNineHoles { get; set; }
    
    [Display(Name = "Women's Tees")]
    public bool IsWomenTees { get; set; }
}