using FairwayFinder.Features.Data.GolfCourseApi;

namespace FairwayFinder.Features.Services.GolfCourseApi;

public class GolfCourseApiImportState
{
    private readonly object _lock = new();

    public bool IsRunning { get; private set; }
    public GolfCourseApiImportResult? CurrentProgress { get; private set; }
    public GolfCourseApiImportResult? LastResult { get; private set; }
    public bool WasCancelled { get; private set; }

    public void MarkStarted()
    {
        lock (_lock)
        {
            IsRunning = true;
            CurrentProgress = null;
            LastResult = null;
            WasCancelled = false;
        }
    }

    public void UpdateProgress(GolfCourseApiImportResult progress)
    {
        lock (_lock)
        {
            CurrentProgress = progress;
        }
    }

    public void MarkCompleted(GolfCourseApiImportResult result, bool cancelled = false)
    {
        lock (_lock)
        {
            IsRunning = false;
            CurrentProgress = null;
            LastResult = result;
            WasCancelled = cancelled;
        }
    }

    public void MarkFailed(string errorMessage)
    {
        lock (_lock)
        {
            IsRunning = false;
            CurrentProgress = null;
            LastResult = new GolfCourseApiImportResult();
            LastResult.Errors.Add(new GolfCourseApiImportError { Reason = errorMessage });
            WasCancelled = false;
        }
    }
}
