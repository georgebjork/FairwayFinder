namespace FairwayFinder.Features.Services.Interfaces;

public interface IPushNotificationService
{
    Task RegisterDeviceAsync(string userId, string deviceToken, string? deviceName, CancellationToken ct = default);

    Task UnregisterDeviceAsync(string deviceToken, CancellationToken ct = default);

    /// <param name="data">
    /// Custom keys sent alongside the alert, so a tap can open the thing the notification is
    /// about. Without them the app has a banner and no idea what it refers to.
    /// </param>
    Task<int> SendToUserAsync(
        string userId,
        string title,
        string body,
        int? badge = null,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default);
}
