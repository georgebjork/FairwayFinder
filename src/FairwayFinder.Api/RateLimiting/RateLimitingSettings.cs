namespace FairwayFinder.Api.RateLimiting;

/// <summary>
/// Per-client-IP request limits. Each bucket is a fixed window: <see cref="RateLimitWindow.PermitLimit"/>
/// requests per <see cref="RateLimitWindow.WindowSeconds"/>, after which the client gets 429 until
/// the window resets.
/// </summary>
public class RateLimitingSettings
{
    /// <summary>Master switch. Tests turn it off so the shared host's single client IP isn't throttled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Every <c>/api/auth</c> endpoint (login, refresh, register, password reset), on top of
    /// <see cref="Api"/>. Lockout protects one account; this stops one IP guessing across many.
    /// </summary>
    public RateLimitWindow Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>
    /// The anonymous <c>/api/public</c> group, on top of <see cref="Api"/>. Its only real caller is the
    /// Landing site's build, a handful of requests a day; the limit caps anyone scraping or guessing slugs.
    /// </summary>
    public RateLimitWindow PublicProfile { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    /// <summary>Every request that matches an endpoint.</summary>
    public RateLimitWindow Api { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>
    /// Requests that match no endpoint. The app never makes these, so the budget is small and the
    /// window long: it exists to shut out scanners probing for <c>/login</c>, <c>/.env</c> and the like.
    /// </summary>
    public RateLimitWindow Unmatched { get; set; } = new() { PermitLimit = 20, WindowSeconds = 600 };
}

public class RateLimitWindow
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}
