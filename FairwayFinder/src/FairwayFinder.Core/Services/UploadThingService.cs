using FairwayFinder.Core.HttpClients.UploadThing;
using FairwayFinder.Core.Services.Interfaces;

namespace FairwayFinder.Core.Services;

public class UploadThingService : IFileUploadService
{
    private readonly UploadThingHttpClient _httpClient;

    public UploadThingService(UploadThingHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<bool> UploadFile(Stream fileContent)
    {
        throw new NotImplementedException();
    }
}