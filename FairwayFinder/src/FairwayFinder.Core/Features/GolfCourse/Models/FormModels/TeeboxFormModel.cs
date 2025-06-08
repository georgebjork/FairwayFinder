using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.GolfCourse.Models.FormModels;

public class TeeboxFormModel
{
    public long? TeeboxId { get; set; }
    
    [Required]
    public long CourseId { get; set; }
    
    [Display(Name = "Tee Box Name")]
    public string Name { get; set; } = "";
    
    [Required]
    [Display(Name = "Par")]
    public long Par { get; set; }
    
    [Required]
    [Range(1, 100)]
    [Display(Name = "Rating")]
    public decimal Rating { get; set; }
    
    [Required]
    [Range(1, 200)]
    [Display(Name = "Slope")]
    public long Slope { get; set; }
    
    [Required]
    [Range(1, 9000)]
    [Display(Name = "Yardage Out")]
    public long YardageOut { get; set; }
    
    [Range(1, 9000)]
    [Display(Name = "Yardage In")]
    public long YardageIn { get; set; }

    public long Yardage
    {
        get
        {
            if (IsNineHoles)
            {
                return YardageOut;
            }

            return YardageOut + YardageIn;
        }
        set { }
    }

    [Display(Name = "Nine Holes")]
    public bool IsNineHoles { get; set; }
    
    [Display(Name = "Women's Tees")]
    public bool IsWomenTees { get; set; }
    
    public List<HoleFormModel> Holes { get; set; } = new();
}