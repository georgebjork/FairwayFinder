using FairwayFinder.Core.Features.Profile.Models.QueryModel;

namespace FairwayFinder.Core.Features.Profile;

public interface IProfileService
{
    Task<ProfileQueryModel?> GetProfileByEmail(string email);
}

public class ProfileService(IProfileRepository profileRepository) : IProfileService
{
    public async Task<ProfileQueryModel?> GetProfileByEmail(string email)
    {
        return await profileRepository.GetProfileByEmail(email);
    }
}