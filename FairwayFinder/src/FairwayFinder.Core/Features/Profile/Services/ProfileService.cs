using FairwayFinder.Core.Features.Profile.Models.FormModels;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Profile.Services;

public interface IProfileService
{
    public Task<ApplicationUser?> GetProfile(string userId);
    public Task<List<ProfileDocument>> GetProfilePictureRecordsAsync(string userId);
    public Task<ProfileDocument?> GetProfilePictureRecordAsync(string userId);
    public Task<bool> UpdateProfile(string userId, EditProfileFormModel form);
    public Task<bool> CheckUsername(string username);
    public Task<bool> DeleteProfilePictureAsync(string documentId);
}


public class ProfileService : IProfileService
{
    private readonly ILogger<ProfileService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileUploadService _fileUploadService;

    public ProfileService(ILogger<ProfileService> logger, UserManager<ApplicationUser> userManager, IUsernameRetriever usernameRetriever, IDocumentRepository documentRepository, IFileUploadService fileUploadService)
    {
        _logger = logger;
        _userManager = userManager;
        _usernameRetriever = usernameRetriever;
        _documentRepository = documentRepository;
        _fileUploadService = fileUploadService;
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

    public async Task<List<ProfileDocument>> GetProfilePictureRecordsAsync(string userId)
    {
        return await _documentRepository.GetUserProfilePictureRecordsAsync(userId);
    }

    public async Task<ProfileDocument?> GetProfilePictureRecordAsync(string userId)
    {
        return await _documentRepository.GetUserProfilePictureRecordAsync(userId);
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
        
        var profile_update = await _userManager.UpdateAsync(profile);

        if (form.ProfilePicture != null)
        {
            await _fileUploadService.UploadProfilePicture(form.ProfilePicture, userId);
        }
        
        return profile_update.Succeeded;
    }

    public async Task<bool> CheckUsername(string username)
    {
        // The username came from the same person who made the request
        if (_usernameRetriever.Username == username)
        {
            return true;
        }
        
        var user = await _userManager.FindByNameAsync(username);
        return user == null;
    }

    public async Task<bool> DeleteProfilePictureAsync(string documentId)
    {
        var result = await _documentRepository.DeleteProfilePictureAsync(documentId);

        if (result) return result;
        
        _logger.LogError("Failed to delete profile picture with id {documentId}", documentId);
        return false;

    }
}