using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;
using FairwayFinder.Core.Services;

namespace FairwayFinder.Core.Features.Profile;

public interface IProfileService
{
    Task<ProfileQueryModel?> GetProfileByEmail(string email);
    Task<bool> IsHandleAvailable(string handle);
    Task<bool> UpdateProfile(ProfileFormModel form);
}

public class ProfileService(IProfileRepository profileRepository, IUsernameRetriever usernameRetriever) : IProfileService
{
    public async Task<ProfileQueryModel?> GetProfileByEmail(string email)
    {
        return await profileRepository.GetProfileByEmail(email);
    }

    public async Task<bool> IsHandleAvailable(string handle)
    {
        return await profileRepository.IsHandleAvailable(handle);
    }

    public async Task<bool> UpdateProfile(ProfileFormModel form)
    {
        var profile = await GetProfileByEmail(usernameRetriever.Username);
        if (!await IsHandleAvailable(form.Handle) && profile?.Handle != form.Handle) return false;
        
        var model = new ProfileQueryModel
        {
            Email = form.Email,
            FirstName = form.FirstName,
            LastName = form.LastName,
            Handle = form.Handle,
            Id = form.UserId
        };
        var rv = await profileRepository.UpdateProfile(model);
        return rv >= 1;
    }
}