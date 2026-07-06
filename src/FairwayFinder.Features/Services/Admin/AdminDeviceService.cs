using FairwayFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Read-only admin surface over registered push devices: every device across all users, most
/// recently seen first, with the owning player's name/email for display.
/// </summary>
public class AdminDeviceService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
{
    public async Task<List<AdminDeviceListItemDto>> GetAllDevicesAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var devices = await db.UserDevices
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new
            {
                d.DeviceId,
                d.UserId,
                d.DeviceName,
                d.Platform,
                d.IsActive,
                d.CreatedAt,
                d.LastSeenAt
            })
            .ToListAsync();

        var userIds = devices.Select(d => d.UserId).Distinct().ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync();
        var userMap = users.ToDictionary(u => u.Id);

        return devices.Select(d =>
        {
            userMap.TryGetValue(d.UserId, out var u);
            var name = u is null ? "" : $"{u.FirstName} {u.LastName}".Trim();
            var email = u?.Email ?? string.Empty;

            return new AdminDeviceListItemDto
            {
                DeviceId = d.DeviceId,
                DeviceName = d.DeviceName,
                Platform = d.Platform,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                LastSeenAt = d.LastSeenAt,
                UserId = d.UserId,
                PlayerName = string.IsNullOrWhiteSpace(name)
                    ? (string.IsNullOrWhiteSpace(email) ? "Unknown" : email)
                    : name,
                PlayerEmail = email
            };
        }).ToList();
    }
}
