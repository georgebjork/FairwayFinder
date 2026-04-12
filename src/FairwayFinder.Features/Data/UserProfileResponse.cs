using FairwayFinder.Identity;

namespace FairwayFinder.Features.Data;

public class UserProfileResponse
{
    public long UserProfileId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid PublicIdentifier { get; set; }
    public bool IsPublic { get; set; }
    public string? DisplayName { get; set; }
    public PreferredTees PreferredTees { get; set; }
}
