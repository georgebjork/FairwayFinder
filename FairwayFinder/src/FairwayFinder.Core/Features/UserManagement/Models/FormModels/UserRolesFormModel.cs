using System.ComponentModel.DataAnnotations;

namespace FairwayFinder.Core.Features.UserManagement.Models.FormModels;

public class UserRolesFormModel
{
    [Required]
    public string? UserId { get; set; }

    [Required] 
    public string SelectRole { get; set; } = "";

    public List<string> AvailableRoles { get; set; } = [];
    public List<string> CurrentRoles { get; set; } = [];
}