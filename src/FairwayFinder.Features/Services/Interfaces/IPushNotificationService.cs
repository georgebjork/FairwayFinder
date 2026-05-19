namespace FairwayFinder.Features.Services.Interfaces;

public interface IPushNotificationService
{
    Task RegisterDeviceAsync(string userId, string deviceToken, string? deviceName, CancellationToken ct = default);

    Task UnregisterDeviceAsync(string deviceToken, CancellationToken ct = default);

    Task<int> SendToUserAsync(string userId, string title, string body, int? badge = null, CancellationToken ct = default);
}
