namespace FairwayFinder.Data.Entities;

public class UserDevice
{
    public long DeviceId { get; set; }

    public string UserId { get; set; } = null!;

    public string DeviceToken { get; set; } = null!;

    public string? DeviceName { get; set; }

    public string Platform { get; set; } = "ios";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public bool IsActive { get; set; }
}
