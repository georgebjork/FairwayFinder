using FairwayFinder.Core.Identity;

namespace FairwayFinder.Core.Features.UserManagement.Models.ViewModels;

public class EditUserViewModel
{
    public ApplicationUser User { get; set; } = new();
}