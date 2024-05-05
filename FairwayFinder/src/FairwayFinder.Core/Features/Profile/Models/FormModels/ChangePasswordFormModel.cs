using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.Profile.Models.FormModels;

public class ChangePasswordFormModel {
    
    [Required]
    public string UserId { get; set; } = "";
    
    [Required]
    [DisplayName("Old Password")]
    [DataType(DataType.Password)]
    public string OldPassword { get; set; } = "";
    
    [Required]
    [DisplayName("New Password")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";
    
    [Required]
    [DisplayName("Confirm Password")]
    [DataType(DataType.Password)]
    public string NewPasswordConfirm { get; set; } = "";
}