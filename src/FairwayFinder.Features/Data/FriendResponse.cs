namespace FairwayFinder.Features.Data;

public class FriendResponse
{
    public long FriendshipId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid PublicIdentifier { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public DateOnly FriendsSince { get; set; }
}
