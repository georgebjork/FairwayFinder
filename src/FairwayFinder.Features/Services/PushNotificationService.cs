using dotAPNS;
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Features.Services;

public class PushNotificationService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IApnsClient apnsClient,
    IOptions<ApnsSettings> apnsSettings,
    ILogger<PushNotificationService> logger) : IPushNotificationService
{
    private readonly ApnsSettings _settings = apnsSettings.Value;

    public async Task RegisterDeviceAsync(string userId, string deviceToken, string? deviceName, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var existing = await db.UserDevices.FirstOrDefaultAsync(d => d.DeviceToken == deviceToken, ct);

        if (existing is null)
        {
            db.UserDevices.Add(new UserDevice
            {
                UserId = userId,
                DeviceToken = deviceToken,
                DeviceName = deviceName,
                Platform = "ios",
                CreatedAt = now,
                UpdatedAt = now,
                LastSeenAt = now,
                IsActive = true
            });
        }
        else
        {
            existing.UserId = userId;
            existing.DeviceName = deviceName ?? existing.DeviceName;
            existing.LastSeenAt = now;
            existing.UpdatedAt = now;
            existing.IsActive = true;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UnregisterDeviceAsync(string deviceToken, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var device = await db.UserDevices.FirstOrDefaultAsync(d => d.DeviceToken == deviceToken, ct);
        if (device is null) return;

        device.IsActive = false;
        device.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> SendToUserAsync(string userId, string title, string body, int? badge = null, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var devices = await db.UserDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(ct);

        if (devices.Count == 0) return 0;

        var sent = 0;
        var now = DateTime.UtcNow;

        foreach (var device in devices)
        {
            var push = new ApplePush(ApplePushType.Alert)
                .AddToken(device.DeviceToken)
                .AddAlert(title, body);

            if (badge.HasValue)
                push.AddBadge(badge.Value);

            if (_settings.UseSandbox)
                push.SendToDevelopmentServer();

            try
            {
                var response = await apnsClient.SendAsync(push, ct);
                if (response.IsSuccessful)
                {
                    sent++;
                    continue;
                }

                if (response.Reason is ApnsResponseReason.BadDeviceToken
                    or ApnsResponseReason.Unregistered
                    or ApnsResponseReason.DeviceTokenNotForTopic)
                {
                    device.IsActive = false;
                    device.UpdatedAt = now;
                    logger.LogInformation("Marking device {DeviceId} inactive due to APNS reason {Reason}", device.DeviceId, response.Reason);
                }
                else
                {
                    logger.LogWarning("APNS send failed for device {DeviceId}: {Reason} {ReasonString}", device.DeviceId, response.Reason, response.ReasonString);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending push to device {DeviceId}", device.DeviceId);
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);

        return sent;
    }
}
