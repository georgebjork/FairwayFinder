using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.GolfCourse.Models.FormModels;

public class CourseFormModel
{
    public long? CourseId { get; set; }
    
    [Display(Name = "Course Name")]
    [Required(ErrorMessage = "Course Name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters.")]
    public string Name { get; set; } = "";

    [Display(Name = "Address")]
    [Required(ErrorMessage = "Address is required.")]
    [StringLength(200, ErrorMessage = "Address cannot be longer than 200 characters.")]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "Phone number is required.")]
    [Display(Name = "Phone Number")]
    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(20, ErrorMessage = "Phone number cannot be longer than 20 characters.")]
    public string PhoneNumber { get; set; } = "";
}