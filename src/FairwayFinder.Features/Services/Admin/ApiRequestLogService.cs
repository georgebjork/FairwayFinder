using FairwayFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Read + retention surface over the API request log. Consumed by the admin dashboard (paged
/// read + manual purge) and by the API's daily purge background service.
/// </summary>
public class ApiRequestLogService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
{
    /// <summary>
    /// Server-side paged read, newest first. Applies optional filters, then returns the requested
    /// window of items plus the total matching count for the pager.
    /// </summary>
    public async Task<PagedResult<ApiRequestLogListItemDto>> GetLogsAsync(
        int skip, int top, string? search = null, int? statusCode = null,
        int? statusClass = null, DateTime? from = null, DateTime? to = null)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var query = db.ApiRequestLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                EF.Functions.ILike(l.Path, $"%{term}%") ||
                (l.UserEmail != null && EF.Functions.ILike(l.UserEmail, $"%{term}%")) ||
                (l.RouteTemplate != null && EF.Functions.ILike(l.RouteTemplate, $"%{term}%")));
        }

        if (statusCode.HasValue)
        {
            query = query.Where(l => l.StatusCode == statusCode.Value);
        }
        else if (statusClass.HasValue)
        {
            var lower = statusClass.Value * 100;
            var upper = lower + 100;
            query = query.Where(l => l.StatusCode >= lower && l.StatusCode < upper);
        }

        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.ApiRequestLogId)
            .Skip(skip)
            .Take(top)
            .Select(l => new ApiRequestLogListItemDto
            {
                ApiRequestLogId = l.ApiRequestLogId,
                Timestamp = l.Timestamp,
                Method = l.Method,
                Path = l.Path,
                RouteTemplate = l.RouteTemplate,
                QueryString = l.QueryString,
                StatusCode = l.StatusCode,
                DurationMs = l.DurationMs,
                UserId = l.UserId,
                UserEmail = l.UserEmail,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                TraceId = l.TraceId
            })
            .ToListAsync();

        return new PagedResult<ApiRequestLogListItemDto> { Items = items, TotalCount = totalCount };
    }

    /// <summary>
    /// Hard-deletes log rows older than <paramref name="retentionDays"/> via a set-based delete
    /// (no entity load). Returns the number of rows removed.
    /// </summary>
    public async Task<int> PurgeOlderThanAsync(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Abs(retentionDays));

        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.ApiRequestLogs
            .Where(l => l.Timestamp < cutoff)
            .ExecuteDeleteAsync();
    }
}
