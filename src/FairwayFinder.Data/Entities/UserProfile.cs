namespace FairwayFinder.Data.Entities;

public class UserProfile
{
    public long UserProfileId { get; set; }

    public string UserId { get; set; } = null!;

    public Guid PublicIdentifier { get; set; }

    public bool IsPublic { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
