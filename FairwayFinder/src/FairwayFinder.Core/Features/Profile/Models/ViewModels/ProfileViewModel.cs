using FairwayFinder.Core.Features.Profile.Models.QueryModel;

namespace FairwayFinder.Core.Features.Profile.Models.ViewModels;

public class ProfileViewModel
{
    public ProfileQueryModel ProfileDetails { get; set; } = new();
}