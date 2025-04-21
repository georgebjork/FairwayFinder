using System.Net.Http.Headers;
using System.Net.Http.Json;
using FairwayFinder.Core.HttpClients.UploadThing.Models;
using FairwayFinder.Core.HttpClients.UploadThing.Models.Requests;
using FairwayFinder.Core.HttpClients.UploadThing.Models.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.HttpClients.UploadThing;

public class UploadThingHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UploadThingHttpClient> _logger;

    public UploadThingHttpClient(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<UploadThingHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.uploadthing.com/v7/");

        var api_key = configuration["UploadThing:ApiKey"];
        if (string.IsNullOrWhiteSpace(api_key))
        {
            throw new InvalidOperationException("UploadThing API key is missing in configuration.");
        }

        _httpClient.DefaultRequestHeaders.Add("x-uploadthing-api-key", api_key);
    }

    public async Task<UploadThingFileUploadResponse> Upload(UploadThingRequest request)
    {
        // Get our presigned url
        var presigned_url = await GetPresignedUrl(request);
        if (presigned_url == null)
        {
            return new UploadThingFileUploadResponse();
        }

        // Set our metadata
        var metadata_set = await SetMetadataBeforeUpload(request, presigned_url);
        if (!metadata_set)
        {
            return new UploadThingFileUploadResponse();
        }
        
        // Upload file
        return await UploadFile(request, presigned_url.Url); 
    }

    private async Task<UploadThingPresignedUrlResponse?> GetPresignedUrl(UploadThingRequest request)
    {
        var presigned_url_request = new UploadThingPresignedUrlRequest
        {
            FileName = request.FileName,
            FileSize = request.ImageStream.Length,
            FileType = request.FileType,
            Slug = request.Slug.ToString().ToLower(),
            CustomId = request.FileId,
            Acl = "public-read",
            ExpiresIn = 3600,
            ContentDisposition = "inline"
        };

        var response = await _httpClient.PostAsJsonAsync("prepareUpload", presigned_url_request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to get presigned URL: {error}");
            return null;
        }

        var presigned_url = await response.Content.ReadFromJsonAsync<UploadThingPresignedUrlResponse>();
        if (presigned_url != null && !string.IsNullOrEmpty(presigned_url.Url))
        {
            return presigned_url;
        }

        _logger.LogError("Presigned URL came back null.");
        return null;
    }

    private async Task<bool> SetMetadataBeforeUpload(UploadThingRequest request, UploadThingPresignedUrlResponse presignedUrl)
    {
        var ingest_uri = new Uri(presignedUrl.Url);
        var region_alias = ingest_uri.Host.Split('.')[0];
        var metadata_endpoint = $"https://{region_alias}.ingest.uploadthing.com/route-metadata";

        var metadata_request = new UploadThingMetadataRequest
        {
            FileKeys = [presignedUrl.Key],
            CallbackSlug = request.Slug.ToString().ToLower(),
            AwaitServerData = false,
            IsDev = false
        };

        var response = await _httpClient.PostAsJsonAsync(metadata_endpoint, metadata_request);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var response_body = await response.Content.ReadAsStringAsync();
        _logger.LogError("UploadThing rejected metadata request: {Body}", response_body);
        return false;
    }

    private async Task<UploadThingFileUploadResponse> UploadFile(UploadThingRequest request, string presignedUrl)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var file_content = new StreamContent(request.ImageStream)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            };
            form.Add(file_content, "file", request.FileName);

            var response = await _httpClient.PutAsync(presignedUrl, form);
            if (!response.IsSuccessStatusCode)
            {
                var response_body = await response.Content.ReadAsStringAsync();
                _logger.LogError("UploadThing rejected file upload: {Body}", response_body);
                return new UploadThingFileUploadResponse { ErrorMessage = response_body };
            }

            var success_response = await response.Content.ReadFromJsonAsync<UploadThingFileUploadResponse>();
            if (success_response == null)
            {
                return new UploadThingFileUploadResponse();
            }

            success_response.IsSuccess = true;
            success_response.FileId = request.FileId;
            success_response.Slug = request.Slug;
            return success_response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during file upload: {ex}");
            return new UploadThingFileUploadResponse();
        }
    }
}
