using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;

namespace FairwayFinder.Core.Features.Profile.Models.FormModels;

public class ProfileFormModel
{
    public string UserId { get; set; } = "";
    
    [Required]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";
    
    [Required]
    [MaxLength(250, ErrorMessage = "Maximum length is 250 characters.")]
    [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "First Name can only contain letters, spaces, apostrophes, and hyphens.")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = "";
    
    [Required]
    [MaxLength(250, ErrorMessage = "Maximum length is 250 characters.")]
    [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "Last Name can only contain letters, spaces, apostrophes, and hyphens.")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = "";
    
    [Required]
    [MaxLength(30, ErrorMessage = "Maximum length is 30 characters.")]
    [Display(Name = "UserName")]
    public string UserName { get; set; } = "";
    public bool IsValidHandle { get; set; } = true;
    
    public string BaseUrl { get; set; } = "";


    public ProfileFormModel ToForm(ProfileQueryModel profile)
    {
        var form = new ProfileFormModel
        {
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            UserName = profile.UserName,
            Email = profile.Email
        };
        return form;
    }
}