using System.Text.Json.Serialization;

namespace FairwayFinder.Core.HttpClients.UploadThing.Models.Responses;

public record UploadThingPresignedUrlResponse
{
    [JsonPropertyName("key")] 
    public string Key { get; set; } = "";

    [JsonPropertyName("url")] 
    public string Url { get; set; } = "";
}