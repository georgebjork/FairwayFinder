using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Profile;

public interface IMyProfileService
{
    Task<ProfileQueryModel?> GetProfile(string email);
    Task<bool> IsHandleAvailable(string handle);
    Task<bool> UpdateProfile(ProfileFormModel form);
    Task<string> GenerateUserName(string firstName, string lastName);
}

public class MyProfileService(IProfileRepository profileRepository, IUsernameRetriever usernameRetriever, ILogger<MyProfileService> logger) : IMyProfileService
{
    public async Task<ProfileQueryModel?> GetProfile(string email)
    {
        return await profileRepository.GetProfileByEmail(email);
    }

    public async Task<bool> IsHandleAvailable(string handle)
    {
        return await profileRepository.IsHandleAvailable(handle);
    }

    public async Task<string> GenerateUserName(string firstName, string lastName)
    {
        var baseHandle = $"{firstName.ToLower()}{lastName.ToLower()}";
        var existingHandles = await profileRepository.FindSimilarHandles(baseHandle); 
        
        // If it doesnt exist. Then we are good to use it.
        if (!existingHandles.Contains(baseHandle)) return baseHandle;
        
        string generatedHandle;
        var counter = 1;
        do // Keep checking with a number appended to see if it is good to use.
        {
            generatedHandle = $"{baseHandle}{counter++}";
            
        } while (existingHandles.Contains(generatedHandle));
        
        // Return
        return generatedHandle;
    }

    public async Task<bool> UpdateProfile(ProfileFormModel form)
    {
        var profile = await GetProfile(usernameRetriever.Username);
        if (!await IsHandleAvailable(form.Handle) && profile?.Handle != form.Handle) return false;

        try
        {
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
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Profile id {id} could not be updated.", form.UserId);
            return false;
        }
        
    }
}