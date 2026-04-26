namespace FairwayFinder.Features.Data;

public class UserSearchResultResponse
{
    public string UserId { get; set; } = string.Empty;
    public Guid PublicIdentifier { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public FriendshipState FriendshipState { get; set; }
    public long? FriendshipId { get; set; }
}

public enum FriendshipState
{
    None = 0,
    PendingOutgoing = 1,
    PendingIncoming = 2,
    Accepted = 3
}
