using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Profile.Services;

public interface IProfileService
{
    public Task<ApplicationUser?> GetProfile(string userId);
    public Task<bool> UpdateProfile(string userId, EditProfileFormModel form);
    public Task<bool> CheckUsername(string username);
}


public class ProfileService : IProfileService
{
    private readonly ILogger<ProfileService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUsernameRetriever _usernameRetriever;

    public ProfileService(ILogger<ProfileService> logger, UserManager<ApplicationUser> userManager, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _userManager = userManager;
        _usernameRetriever = usernameRetriever;
    }

    public async Task<ApplicationUser?> GetProfile(string userId) 
    {
        if (!Guid.TryParse(userId, out var guid))
        {
            _logger.LogError("Invalid guid was passed in");
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId);
        return user;
    }

    public async Task<bool> UpdateProfile(string userId, EditProfileFormModel form)
    {
        // Get profile and validate it exists
        var profile = await GetProfile(userId);
        if (profile is null) return false;
        
        // Validate username is available
        if (!await CheckUsername(form.Username)) return false;

        profile.FirstName = form.FirstName;
        profile.LastName = form.LastName;
        profile.UserName = form.Username;
        profile.Email = form.Email;
        
        var rv = await _userManager.UpdateAsync(profile);
        return rv.Succeeded;
    }

    public async Task<bool> CheckUsername(string username)
    {
        // The username came from the same person who made the request
        if (_usernameRetriever.Username == username) return true;
        
        var user = await _userManager.FindByNameAsync(username);
        return user == null;
    }
}