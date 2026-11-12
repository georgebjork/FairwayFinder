namespace FairwayFinder.Features.Data;

public class UsernameAvailability
{
    /// <summary>The normalized form of what was asked about (lowercased, leading "@" dropped).</summary>
    public string Username { get; set; } = string.Empty;
    public bool Available { get; set; }

    /// <summary>Why it isn't available: invalid, reserved or taken. Null when available.</summary>
    public string? Reason { get; set; }
}

public enum UsernameChangeStatus
{
    Ok,
    Invalid,
    Taken,
    NotFound
}

/// <summary>
/// Outcome of a username change. A result rather than an exception because Features can't throw
/// the API's exception types; the endpoint maps the status to HTTP.
/// </summary>
public record UsernameChangeResult(UsernameChangeStatus Status, string Username, string? Error = null)
{
    public bool Success => Status == UsernameChangeStatus.Ok;
}
