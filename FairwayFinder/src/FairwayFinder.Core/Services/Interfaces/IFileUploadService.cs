using Microsoft.AspNetCore.Http;

namespace FairwayFinder.Core.Services.Interfaces;

public interface IFileUploadService
{
    public Task<bool> UploadProfilePicture(IFormFile file, string userId);
}