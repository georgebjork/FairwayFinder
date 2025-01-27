using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.CourseManagement.Models.FormModels;

public class CourseFormModel
{
    public long? course_id { get; set; }
    
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters.")]
    public string name { get; set; } = "";

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(200, ErrorMessage = "Address cannot be longer than 200 characters.")]
    public string address { get; set; } = "";

    [Required(ErrorMessage = "Phone number is required.")]
    [Display(Name = "Phone Number")]
    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(20, ErrorMessage = "Phone number cannot be longer than 20 characters.")]
    public string phone_number { get; set; } = "";
}