using FairwayFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Read + retention surface over the Warning-or-worse application log. Consumed by the admin
/// console's error log page and by its daily purge background service.
/// </summary>
public class AppLogService(IDbContextFactory<LogDbContext> dbContextFactory)
{
    /// <summary>
    /// Server-side paged read, newest first. Applies optional filters, then returns the requested
    /// window of items plus the total matching count for the pager.
    /// </summary>
    public async Task<PagedResult<AppLogListItemDto>> GetLogsAsync(
        int skip, int top, string? search = null, string? level = null,
        string? application = null, DateTime? from = null, DateTime? to = null)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var query = db.AppLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                EF.Functions.ILike(l.Message, $"%{term}%") ||
                (l.UserEmail != null && EF.Functions.ILike(l.UserEmail, $"%{term}%")) ||
                (l.SourceContext != null && EF.Functions.ILike(l.SourceContext, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(l => l.Level == level);

        if (!string.IsNullOrWhiteSpace(application))
            query = query.Where(l => l.Application == application);

        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.AppLogId)
            .Skip(skip)
            .Take(top)
            .Select(l => new AppLogListItemDto
            {
                AppLogId = l.AppLogId,
                Timestamp = l.Timestamp,
                Level = l.Level,
                Message = l.Message,
                SourceContext = l.SourceContext,
                Application = l.Application,
                UserId = l.UserId,
                UserEmail = l.UserEmail,
                HasException = l.Exception != null
            })
            .ToListAsync();

        return new PagedResult<AppLogListItemDto> { Items = items, TotalCount = totalCount };
    }

    /// <summary>The full entry, including the stack trace and properties; null if it was purged.</summary>
    public async Task<AppLogDetailDto?> GetLogDetailAsync(long appLogId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.AppLogs
            .AsNoTracking()
            .Where(l => l.AppLogId == appLogId)
            .Select(l => new AppLogDetailDto
            {
                AppLogId = l.AppLogId,
                Timestamp = l.Timestamp,
                Level = l.Level,
                Message = l.Message,
                SourceContext = l.SourceContext,
                Application = l.Application,
                UserId = l.UserId,
                UserEmail = l.UserEmail,
                HasException = l.Exception != null,
                MessageTemplate = l.MessageTemplate,
                Exception = l.Exception,
                RequestPath = l.RequestPath,
                TraceId = l.TraceId,
                Properties = l.Properties
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>The distinct hosts that have logged, for the application filter.</summary>
    public async Task<List<string>> GetApplicationsAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.AppLogs
            .Select(l => l.Application)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }

    /// <summary>
    /// Hard-deletes entries older than <paramref name="retentionDays"/> via a set-based delete
    /// (no entity load). Returns the number of rows removed.
    /// </summary>
    public async Task<int> PurgeOlderThanAsync(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Abs(retentionDays));

        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.AppLogs
            .Where(l => l.Timestamp < cutoff)
            .ExecuteDeleteAsync();
    }
}
