using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.CourseManagement.Models.FormModels;

public class CourseFormModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(200, ErrorMessage = "Address cannot be longer than 200 characters.")]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "Phone number is required.")]
    [Display(Name = "Phone Number")]
    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(20, ErrorMessage = "Phone number cannot be longer than 20 characters.")]
    public string PhoneNumber { get; set; } = "";
}