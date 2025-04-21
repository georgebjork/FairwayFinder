using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.Profile.Models.FormModels;

public class EditProfileFormModel
{
    [Display(Name = "First Name")]
    [Required(ErrorMessage = "First Name is required.")]
    [StringLength(50, ErrorMessage = "First Name cannot be longer than 50 characters.")]
    public string FirstName { get; set; } = "";
    
    [Display(Name = "Last Name")]
    [Required(ErrorMessage = "Last Name is required.")]
    [StringLength(50, ErrorMessage = "Last Name cannot be longer than 50 characters.")]
    public string LastName { get; set; } = "";
    
    [EmailAddress]
    [Display(Name = "Email")]
    [Required(ErrorMessage = "Email is required.")]
    [StringLength(256, ErrorMessage = "Email cannot be longer than 256 characters.")]
    public string Email { get; set; } = "";
    
    [Display(Name = "Username")]
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, ErrorMessage = "Email cannot be longer than 50 characters.")]
    public string Username { get; set; } = "";

    public bool? IsValidUsername { get; set; }
}