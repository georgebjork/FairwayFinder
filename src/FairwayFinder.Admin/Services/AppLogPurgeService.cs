using FairwayFinder.Features.Services.Admin;
using FairwayFinder.Shared.Settings;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Admin.Services;

/// <summary>
/// Enforces the application-log retention window. Runs once at startup and then daily, hard-deleting
/// entries older than <see cref="AppLoggingSettings.RetentionDays"/>.
/// </summary>
public class AppLogPurgeService(
    IServiceScopeFactory scopeFactory,
    IOptions<AppLoggingSettings> options,
    ILogger<AppLogPurgeService> logger) : BackgroundService
{
    private readonly AppLoggingSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        do
        {
            await PurgeAsync();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AppLogService>();
            var deleted = await service.PurgeOlderThanAsync(_settings.RetentionDays);

            if (deleted > 0)
                logger.LogInformation("Purged {Count} application log entries older than {Days} days",
                    deleted, _settings.RetentionDays);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Application log purge failed");
        }
    }
}
