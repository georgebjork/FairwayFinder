using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Repositories.Interfaces;

public interface IDocumentRepository : IBaseRepository
{
    public Task<List<ProfileDocument>> GetUserProfilePictureRecordsAsync(string userId);
    public Task<ProfileDocument?> GetUserProfilePictureRecordAsync(string userId);
    public Task<bool> DeleteProfilePictureAsync(string documentId);
}