using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.UserManagement.Models.FormModels;

public class InviteUserFormModel
{
    [Required(ErrorMessage="An Email is Required")]
    [EmailAddress]
    public string Email { get; set; } = "";
}