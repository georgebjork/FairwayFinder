using System.Threading.Channels;
using FairwayFinder.Features.Data.GolfCourseApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services.GolfCourseApi;

public class GolfCourseApiImportJob : BackgroundService
{
    private readonly Channel<bool> _trigger;
    private readonly GolfCourseApiImportState _state;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GolfCourseApiImportJob> _logger;

    private CancellationTokenSource? _importCts;

    public GolfCourseApiImportJob(
        Channel<bool> trigger,
        GolfCourseApiImportState state,
        IServiceScopeFactory scopeFactory,
        ILogger<GolfCourseApiImportJob> logger)
    {
        _trigger = trigger;
        _state = state;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Signal the background job to start an import. Returns false if already running.
    /// </summary>
    public bool TriggerImport()
    {
        if (_state.IsRunning) return false;
        _trigger.Writer.TryWrite(true);
        return true;
    }

    /// <summary>
    /// Request cancellation of the currently running import.
    /// </summary>
    public void CancelImport()
    {
        _importCts?.Cancel();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GolfCourseApiImportJob started, waiting for triggers");

        await foreach (var _ in _trigger.Reader.ReadAllAsync(stoppingToken))
        {
            if (_state.IsRunning)
            {
                _logger.LogWarning("Import trigger received but import is already running, ignoring");
                continue;
            }

            _state.MarkStarted();
            _importCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            try
            {
                _logger.LogInformation("Background import triggered");

                using var scope = _scopeFactory.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<GolfCourseApiImportService>();

                var progress = new Progress<GolfCourseApiImportResult>(result =>
                {
                    _state.UpdateProgress(result);
                });

                var result = await importService.ImportAllCoursesAsync(progress, _importCts.Token);
                _state.MarkCompleted(result);

                _logger.LogInformation("Background import completed: {Imported} imported, {Updated} updated, {Errors} errors",
                    result.CoursesImported, result.CoursesUpdated, result.Errors.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background import was cancelled");
                var progress = _state.CurrentProgress;
                _state.MarkCompleted(progress ?? new GolfCourseApiImportResult(), cancelled: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background import failed with an unhandled exception");
                _state.MarkFailed(ex.Message);
            }
            finally
            {
                _importCts?.Dispose();
                _importCts = null;
            }
        }
    }
}
