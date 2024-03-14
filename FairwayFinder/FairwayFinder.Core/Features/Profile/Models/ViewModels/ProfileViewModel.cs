using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;

namespace FairwayFinder.Core.Features.Profile.Models.ViewModels;

public class ProfileViewModel
{
    public ProfileQueryModel Profile { get; set; } = new();
    public ProfileFormModel Form { get; set; } = new();

}