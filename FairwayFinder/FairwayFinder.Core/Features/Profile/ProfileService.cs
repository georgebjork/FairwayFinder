using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;

namespace FairwayFinder.Core.Features.Profile;

public interface IProfileService
{
    Task<ProfileQueryModel?> GetProfileByEmail(string email);
    Task<bool> IsHandleAvailable(string handle);
    Task<bool> UpdateProfile(ProfileFormModel form);
}

public class ProfileService(IProfileRepository profileRepository) : IProfileService
{
    public async Task<ProfileQueryModel?> GetProfileByEmail(string email)
    {
        return await profileRepository.GetProfileByEmail(email);
    }

    public async Task<bool> IsHandleAvailable(string handle)
    {
        return await profileRepository.IsHandleAvailable(handle);
    }

    public Task<bool> UpdateProfile(ProfileFormModel form)
    {
        throw new NotImplementedException();
    }
}