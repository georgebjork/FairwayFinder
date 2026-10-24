using FairwayFinder.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace FairwayFinder.Features.Services.Logging;

/// <summary>
/// Writes batches of log events to the logs database. Serilog's batching wrapper calls this from its
/// own background loop and owns buffering and retries; a batch that throws is retried, then dropped.
/// </summary>
public class AppLogBatchWriter(IServiceProvider services, string application) : IBatchedLogEventSink
{
    // Resolved on the first batch, not in the constructor: EF's options factory resolves
    // ILoggerFactory, which is Serilog — and Serilog resolves this sink while it is being built.
    private IDbContextFactory<LogDbContext>? _dbContextFactory;

    public Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch) => LogWriteScope.RunAsync(async () =>
    {
        _dbContextFactory ??= services.GetRequiredService<IDbContextFactory<LogDbContext>>();

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        db.AppLogs.AddRange(batch.Select(e => AppLogEntryMapper.From(e, application)));
        await db.SaveChangesAsync();
    });

    public Task OnEmptyBatchAsync() => Task.CompletedTask;
}
