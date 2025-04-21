using System.Text.Json.Serialization;
using FairwayFinder.Core.HttpClients.UploadThing.Models.Slugs;

namespace FairwayFinder.Core.HttpClients.UploadThing.Models.Responses;

public class UploadThingFileUploadResponse
{
    [JsonPropertyName("ufsUrl")]
    public string UfsUrl { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("appUrl")]
    public string AppUrl { get; set; } = "";

    [JsonPropertyName("fileHash")]
    public string FileHash { get; set; } = "";

    public string FileId { get; set; } = "";
    public UploadThingSlugs Slug { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}