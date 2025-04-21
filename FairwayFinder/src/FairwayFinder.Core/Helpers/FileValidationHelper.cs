namespace FairwayFinder.Core.Helpers;

public static class FileValidationHelper
{
    private static readonly HashSet<string> _allowed_image_mime_types =
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    ];

    public static bool IsImageFile(string contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) && _allowed_image_mime_types.Contains(contentType.ToLowerInvariant());
    }
}