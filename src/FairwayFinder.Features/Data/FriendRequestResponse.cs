namespace FairwayFinder.Features.Data;

public class FriendRequestResponse
{
    public long FriendshipId { get; set; }
    public string OtherUserId { get; set; } = string.Empty;
    public Guid OtherPublicIdentifier { get; set; }
    public string OtherDisplayName { get; set; } = string.Empty;
    public string OtherEmail { get; set; } = string.Empty;
    public FriendRequestDirection Direction { get; set; }
    public DateOnly RequestedOn { get; set; }
}

public enum FriendRequestDirection
{
    Incoming = 0,
    Outgoing = 1
}
