using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.Admin.UserManagement.Models.FormModels;

public class UserInviteFormModel
{
    [Required(ErrorMessage="An Email is Required")]
    [EmailAddress]
    public string Email { get; set; } = "";
}