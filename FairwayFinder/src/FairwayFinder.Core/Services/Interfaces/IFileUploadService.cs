namespace FairwayFinder.Core.Services.Interfaces;

public interface IFileUploadService
{
    public Task<bool> UploadFile(Stream fileContent);
}