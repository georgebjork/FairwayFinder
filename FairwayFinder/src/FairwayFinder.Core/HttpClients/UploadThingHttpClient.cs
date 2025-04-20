using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace FairwayFinder.Core.HttpClients;

public class UploadThingHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public UploadThingHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _httpClient.BaseAddress = new Uri("https://api.uploadthing.com/v6/");
        
        var apiKey = _configuration["UploadThing:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("UploadThing API key is missing in configuration.");
        }

        _httpClient.DefaultRequestHeaders.Add("x-uploadthing-api-key", apiKey);
    }

    public async Task ListFiles()
    {
        var response = await _httpClient.PostAsJsonAsync("listFiles", new {});

        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine(body);
    }
    
    public async Task<bool> Upload(Stream imageStream, string fileName, string fileType)
    {
        // Create the request payload
        var request = new UploadThingRequestBody
        {
            FileName = fileName,
            FileSize = imageStream.Length,
            FileType = fileType,
            Slug = "profile", // Consider making this configurable if needed
            CustomId = Guid.NewGuid().ToString(),
            Acl = "public-read"
        };

        // Use built-in extension method to post JSON content
        var response = await _httpClient.PostAsJsonAsync("prepareUpload", request);

        // Optional: log or handle the response body if needed
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseBody);

        return response.IsSuccessStatusCode;
    }

    
}

public record UploadThingRequestBody
{
    [JsonPropertyName("fileName")] 
    public string FileName { get; set; } = "";

    [JsonPropertyName("fileSize")] 
    public long FileSize { get; set; }
    
    [JsonPropertyName("slug")] 
    public string Slug { get; set; }
    
    [JsonPropertyName("fileType")] 
    public string FileType { get; set; } = "";
        
    [JsonPropertyName("customId")] 
    public string CustomId { get; set; } = "";

    [JsonPropertyName("contentDisposition")]
    public string ContentDisposition { get; set; } = "";
        
    [JsonPropertyName("acl")] 
    public string Acl { get; set; } = "";
        
    [JsonPropertyName("expiresIn")] 
    public int ExpiresIn { get; set; }
}