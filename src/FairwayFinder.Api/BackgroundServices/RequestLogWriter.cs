using System.Threading.Channels;
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Api.BackgroundServices;

/// <summary>
/// Drains the request-log channel and batch-inserts entries into the database, decoupling the DB
/// write from request handling. Mirrors the codebase's channel + BackgroundService pattern.
/// </summary>
public class RequestLogWriter(
    Channel<ApiRequestLog> channel,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<RequestLogWriter> logger) : BackgroundService
{
    private const int MaxBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RequestLogWriter started");

        try
        {
            // Wait for at least one item, then drain whatever else is immediately available.
            while (await channel.Reader.WaitToReadAsync(stoppingToken))
            {
                var batch = new List<ApiRequestLog>(MaxBatchSize);
                while (batch.Count < MaxBatchSize && channel.Reader.TryRead(out var entry))
                {
                    batch.Add(entry);
                }

                if (batch.Count == 0) continue;

                try
                {
                    await using var db = await dbContextFactory.CreateDbContextAsync(stoppingToken);
                    db.ApiRequestLogs.AddRange(batch);
                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to persist {Count} request log entries", batch.Count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down — expected.
        }

        logger.LogInformation("RequestLogWriter stopped");
    }
}
