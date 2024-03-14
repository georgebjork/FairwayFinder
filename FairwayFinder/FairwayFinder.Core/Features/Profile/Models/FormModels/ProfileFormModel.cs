using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;

namespace FairwayFinder.Core.Features.Profile.Models.FormModels;

public class ProfileFormModel
{
    [Required]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";
    
    [Required]
    [MaxLength(250, ErrorMessage = "Maximum length is 250 characters.")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = "";
    
    [Required]
    [MaxLength(250, ErrorMessage = "Maximum length is 250 characters.")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = "";
    
    [Required]
    [MaxLength(30, ErrorMessage = "Maximum length is 30 characters.")]
    [Display(Name = "Handle")]
    public string Handle { get; set; } = "";
    
    
    public string BaseUrl { get; set; } = "";


    public ProfileFormModel ToForm(ProfileQueryModel profile)
    {
        var form = new ProfileFormModel
        {
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Handle = profile.Handle,
            Email = profile.Email
        };
        return form;
    }
}