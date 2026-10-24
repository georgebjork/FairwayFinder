namespace FairwayFinder.Features.Services.Logging;

/// <summary>
/// Marks the async flow that is writing log events to the database. Anything logged inside it —
/// an EF or Npgsql failure, say — must not be fed back into the sink, or a failing write feeds itself.
/// </summary>
internal static class LogWriteScope
{
    private static readonly AsyncLocal<bool> Writing = new();

    public static bool IsActive => Writing.Value;

    public static async Task RunAsync(Func<Task> write)
    {
        Writing.Value = true;
        try
        {
            await write();
        }
        finally
        {
            Writing.Value = false;
        }
    }
}
