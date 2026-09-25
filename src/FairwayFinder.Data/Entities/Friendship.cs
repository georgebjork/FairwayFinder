using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

public class Friendship : IAuditable
{
    public long FriendshipId { get; set; }

    public string RequesterUserId { get; set; } = null!;

    public string AddresseeUserId { get; set; } = null!;

    public FriendshipStatus Status { get; set; }

    public DateTime? RespondedOn { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}

public enum FriendshipStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3
}
