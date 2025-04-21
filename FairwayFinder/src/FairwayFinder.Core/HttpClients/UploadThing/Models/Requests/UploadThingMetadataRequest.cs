using System.Text.Json.Serialization;

namespace FairwayFinder.Core.HttpClients.UploadThing.Models;

public class UploadThingMetadataRequest
{
    [JsonPropertyName("fileKeys")]
    public string[] FileKeys { get; set; }

    [JsonPropertyName("metadata")]
    public object Metadata { get; set; } = new { };

    [JsonPropertyName("callbackUrl")]
    public string CallbackUrl { get; set; } = "";

    [JsonPropertyName("callbackSlug")]
    public string CallbackSlug { get; set; } = "profile";

    [JsonPropertyName("awaitServerData")]
    public bool AwaitServerData { get; set; } = false;

    [JsonPropertyName("isDev")]
    public bool IsDev { get; set; } = false;
}