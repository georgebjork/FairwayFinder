using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Identity;

namespace FairwayFinder.Core.Features.Profile.Models.ViewModels;

public class EditProfileViewModel
{
    public EditProfileFormModel Form { get; set; } = new();
    public ApplicationUser User { get; set; } = new();
}