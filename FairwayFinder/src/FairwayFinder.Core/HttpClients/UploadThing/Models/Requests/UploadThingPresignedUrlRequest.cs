using System.Text.Json.Serialization;

namespace FairwayFinder.Core.HttpClients.UploadThing.Models;

public record UploadThingPresignedUrlRequest 
{
    [JsonPropertyName("fileName")] 
    public string FileName { get; set; } = "";

    [JsonPropertyName("fileSize")] 
    public long FileSize { get; set; }
    
    [JsonPropertyName("slug")] 
    public string Slug { get; set; } = "";
    
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