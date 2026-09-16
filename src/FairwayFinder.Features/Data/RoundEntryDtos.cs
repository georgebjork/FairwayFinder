using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Data;

/// <summary>
/// Opens a round for hole-by-hole entry. Carries the round header only — holes arrive one at a
/// time via the per-hole upsert.
/// </summary>
public class StartRoundRequest
{
    public long CourseId { get; set; }
    public long TeeboxId { get; set; }

    /// <summary>The golfer's local date. Sent by the client because UtcNow is a day off for evening rounds.</summary>
    public DateOnly DatePlayed { get; set; }

    public bool FullRound { get; set; }
    public bool FrontNine { get; set; }
    public bool BackNine { get; set; }
    public bool UsingHoleStats { get; set; }
    public bool UsingShotTracking { get; set; }
}

/// <summary>
/// The new round, plus the holes of its teebox. Returning them here saves the client a second
/// call and pins par and hole ids to the exact teebox version the round is on.
/// </summary>
public class StartRoundResponse
{
    public long RoundId { get; set; }
    public long TeeboxId { get; set; }
    public List<HoleInfo> Holes { get; set; } = new();
}

/// <summary>
/// One hole's score and stats. Deliberately carries no HoleId and no Par: both are resolved
/// server-side from the round's teebox and the hole number in the route, so a client can neither
/// post a hole belonging to a different teebox nor invent a par that skews the scoring
/// distribution.
/// </summary>
public class UpsertHoleRequest : IHoleStatSource
{
    public short Score { get; set; }

    // Advanced stats (only read when the round is tracking hole stats)
    public bool? HitFairway { get; set; }
    public long? MissFairwayType { get; set; }
    public bool? HitGreen { get; set; }
    public long? MissGreenType { get; set; }
    public short? NumberOfPutts { get; set; }
    public int? ApproachYardage { get; set; }
    public bool TeeShotOutOfPosition { get; set; }
    public bool ApproachShotOutOfPosition { get; set; }
    public bool TeeShotPenalty { get; set; }
    public bool ApproachShotPenalty { get; set; }

    // Shot-by-shot data (only read when the round is shot-tracked)
    public List<ShotData>? Shots { get; set; }
}

/// <summary>
/// Running state after a hole is written or cleared, so the app can update its scorecard header
/// without refetching the round.
/// </summary>
public class RoundProgressResponse
{
    public long RoundId { get; set; }
    public int HoleNumber { get; set; }
    public long HoleId { get; set; }
    public long ScoreId { get; set; }
    public int Par { get; set; }

    /// <summary>The hole's score, or null once it has been cleared.</summary>
    public short? Score { get; set; }

    public int ScoreOut { get; set; }
    public int ScoreIn { get; set; }
    public int Total { get; set; }
    public int HolesEntered { get; set; }

    /// <summary>Par of the holes entered so far — the only honest baseline mid-round.</summary>
    public int ParEntered { get; set; }

    public int ToPar => Total - ParEntered;
}

/// <summary>
/// How a round-entry call ended. Features cannot reference the API's exception types (the
/// dependency runs Api → Features), so outcomes come back as data and the endpoint layer maps
/// them to status codes.
/// </summary>
public enum RoundEntryStatus
{
    Ok,
    RoundNotFound,
    NotOwner,
    RoundAlreadyComplete,
    HoleNotOnTeebox,
    TeeboxArchived,
    ActiveRoundExists,
    RoundIncomplete
}

/// <summary>Result of a round-entry call: an outcome, and a value when it succeeded.</summary>
public sealed class RoundEntryResult<T>
{
    public RoundEntryStatus Status { get; init; }
    public T? Value { get; init; }

    /// <summary>Set on <see cref="RoundEntryStatus.ActiveRoundExists"/> so the client can offer resume-or-discard.</summary>
    public long? ConflictingRoundId { get; init; }

    /// <summary>Set on <see cref="RoundEntryStatus.RoundIncomplete"/>: the hole numbers still needed.</summary>
    public IReadOnlyList<int> MissingHoles { get; init; } = [];

    public bool IsOk => Status == RoundEntryStatus.Ok;

    public static RoundEntryResult<T> Ok(T value) => new() { Status = RoundEntryStatus.Ok, Value = value };

    public static RoundEntryResult<T> Fail(
        RoundEntryStatus status,
        long? conflictingRoundId = null,
        IReadOnlyList<int>? missingHoles = null)
        => new()
        {
            Status = status,
            ConflictingRoundId = conflictingRoundId,
            MissingHoles = missingHoles ?? []
        };
}
