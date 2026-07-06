using FairwayFinder.Features.Services.Admin;
using FairwayFinder.Shared.Settings;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Api.BackgroundServices;

/// <summary>
/// Enforces the request-log retention window. Runs once at startup and then daily, hard-deleting
/// entries older than <see cref="RequestLoggingSettings.RetentionDays"/>.
/// </summary>
public class RequestLogPurgeService(
    IServiceScopeFactory scopeFactory,
    IOptions<RequestLoggingSettings> options,
    ILogger<RequestLogPurgeService> logger) : BackgroundService
{
    private readonly RequestLoggingSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        do
        {
            await PurgeAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ApiRequestLogService>();
            var deleted = await service.PurgeOlderThanAsync(_settings.RetentionDays);

            if (deleted > 0)
                logger.LogInformation("Purged {Count} request log entries older than {Days} days",
                    deleted, _settings.RetentionDays);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Request log purge failed");
        }
    }
}
