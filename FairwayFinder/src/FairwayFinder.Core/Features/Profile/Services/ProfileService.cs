using FairwayFinder.Core.Features.Profile.Models.ViewModels;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Profile.Services;

public interface IProfileService
{
    public Task<ProfileViewModel?> GetProfile(string handle);
}

public class ProfileService(ILogger<ProfileService> logger, IProfileRepository repository) : IProfileService
{
    
    public async Task<ProfileViewModel?> GetProfile(string handle)
    {
        var profileDetails = await repository.GetProfileByUserName(handle);

        if (profileDetails is not null) return new ProfileViewModel { ProfileDetails = profileDetails };
        
        logger.LogWarning("Profile with {handle} could not be found.", handle);
        return null;
    }
}