namespace FairwayFinder.Features.Services.Interfaces;

public interface IPushNotificationService
{
    Task RegisterDeviceAsync(string userId, string deviceToken, string? deviceName, CancellationToken ct = default);

    /// <summary>
    /// Deactivates the token only if it is registered to <paramref name="userId"/>. A token that is
    /// unknown or belongs to someone else is a silent no-op, so the caller cannot tell the two apart.
    /// </summary>
    Task UnregisterDeviceAsync(string userId, string deviceToken, CancellationToken ct = default);

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
