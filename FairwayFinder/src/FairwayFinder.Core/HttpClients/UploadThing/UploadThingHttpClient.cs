using System.Net.Http.Json;
using FairwayFinder.Core.HttpClients.UploadThing.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.HttpClients.UploadThing;

public class UploadThingHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UploadThingHttpClient> _logger;

    public UploadThingHttpClient(HttpClient httpClient, IConfiguration configuration, ILogger<UploadThingHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.uploadthing.com/v7/");
        
        var apiKey = configuration["UploadThing:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("UploadThing API key is missing in configuration.");
        }

        _httpClient.DefaultRequestHeaders.Add("x-uploadthing-api-key", apiKey);
    }
    
    public async Task<bool> Upload(Stream imageStream, string fileName, string fileType)
    {
        var request = new UploadThingRequest
        {
            FileName = fileName,
            FileSize = imageStream.Length,
            FileType = fileType,
            Slug = "profile",
            CustomId = Guid.NewGuid().ToString(),
            Acl = "public-read",
            ExpiresIn = 3600,
            ContentDisposition = "inline"
        };
        
        // Get the presigned URL
        var presigned_url_response = await _httpClient.PostAsJsonAsync("prepareUpload", request);

        if (!presigned_url_response.IsSuccessStatusCode)
        {
            _logger.LogError($"Failed to get presigned URL: {await presigned_url_response.Content.ReadAsStringAsync()}");
            return false;
        }

        var presigned_url = await presigned_url_response.Content.ReadFromJsonAsync<UploadThingPresignedUrlResponse>();

        if (string.IsNullOrEmpty(presigned_url.Url))
        {
            _logger.LogError("Presigned url came back null.");
            return false;
        }
        
        // Upload the file using the presigned URL
        return await UploadFileToPresignedUrl(imageStream, fileName, presigned_url.Url);
    }

    private async Task<bool> UploadFileToPresignedUrl(Stream imageStream, string fileName, string presignedUrl)
    {
        try 
        {
            // Log before starting
            Console.WriteLine("Starting upload...");
        
            // Use a new client
            using var upload_client = new HttpClient();
        
            var file_content = new StreamContent(imageStream);
            file_content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        
            var multipart_content = new MultipartFormDataContent();
            multipart_content.Add(file_content, "file", fileName);

            Console.WriteLine("About to send request...");
        
            // Upload the file
            var upload_response = await upload_client.PutAsync(presignedUrl, multipart_content);
        
            Console.WriteLine($"Response received: {upload_response.StatusCode}");
        
            if (upload_response.IsSuccessStatusCode) return true;
        
            _logger.LogError($"Failed to upload file: {await upload_response.Content.ReadAsStringAsync()}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            _logger.LogError($"Exception in UploadFileToPresignedUrl: {ex}");
            return false;
        }
    }
}