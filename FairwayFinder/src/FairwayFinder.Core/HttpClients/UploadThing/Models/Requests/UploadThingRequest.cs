using FairwayFinder.Core.HttpClients.UploadThing.Models.Slugs;

namespace FairwayFinder.Core.HttpClients.UploadThing.Models.Requests;

public record UploadThingRequest
{
    public required Stream ImageStream { get; set; }
    public required string FileName { get; set; }
    public required string FileType { get; set; }
    public required UploadThingSlugs Slug { get; set; }

    public string FileId { get; init; } = Guid.NewGuid().ToString();}