using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.HttpClients.UploadThing;
using FairwayFinder.Core.HttpClients.UploadThing.Models.Requests;
using FairwayFinder.Core.HttpClients.UploadThing.Models.Slugs;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FairwayFinder.Core.Services;

public class UploadThingService : IFileUploadService
{
    private readonly UploadThingHttpClient _httpClient;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly IDocumentRepository _documentRepository;
    
    public UploadThingService(UploadThingHttpClient httpClient, IUsernameRetriever usernameRetriever, IDocumentRepository documentRepository)
    {
        _httpClient = httpClient;
        _usernameRetriever = usernameRetriever;
        _documentRepository = documentRepository;
    }

    public async Task<bool> UploadProfilePicture(IFormFile file, string userId)
    {
        if (!FileValidationHelper.IsImageFile(file.ContentType))
        {
            return false;
        }
        
        // Upload our file
        var stream = file.OpenReadStream();
        var upload_thing_response = await _httpClient.Upload( new UploadThingRequest
        {
            ImageStream = stream,
            FileName = file.FileName,
            FileType = file.ContentType,
            Slug = UploadThingSlugs.Profile
        });

        if (!upload_thing_response.IsSuccess)
        {
            return false;
        }
        
        // Create record
        var db_record = new ProfileDocument
        {
            document_id = upload_thing_response.FileId,
            file_url = upload_thing_response.Url,
            user_id = userId,
            route = upload_thing_response.Slug.ToString().ToLower(),
            file_name = file.FileName
        };
        db_record = EntityMetadataHelper.NewRecord(db_record, _usernameRetriever.UserId);
        
        // Insert our record
        var rv = await _documentRepository.Insert(db_record);
        return rv > 0;
    }
}