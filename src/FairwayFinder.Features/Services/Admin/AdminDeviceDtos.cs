namespace FairwayFinder.Features.Services.Admin;

public class AdminDeviceListItemDto
{
    public long DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string Platform { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerEmail { get; set; } = string.Empty;
}
