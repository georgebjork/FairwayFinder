namespace FairwayFinder.Features.Games;

/// <summary>
/// How a game call ended. Features cannot reference the API's exception types (the dependency runs
/// Api → Features), so outcomes come back as data and the endpoint layer maps them to status codes.
/// Mirrors <c>RoundEntryStatus</c>.
/// </summary>
public enum GameResultStatus
{
    Ok,

    GameNotFound,
    NotParticipant,
    NotHost,

    GameNotInSetup,
    GameNotActive,
    GameAlreadyComplete,

    ParticipantNotFound,
    HoleNotInGame,

    RoundNotOwned,
    RoundNotOnGameCourse,
    RoundNotActive,

    JoinCodeInvalid,
    AlreadyJoined,

    ParticipantCountInvalid,
    GameShapeUnsupported,

    /// <summary>The chosen teebox has been superseded by a newer version.</summary>
    TeeboxArchived,

    /// <summary>The chosen teebox belongs to a different course than the game.</summary>
    TeeboxNotOnCourse,

    /// <summary>The teebox does not have every hole the game covers. Detail names the missing ones.</summary>
    TeeboxShapeMismatch,

    /// <summary>
    /// The participant's strokes come from a linked round, so host-entered scores would be
    /// written but never read.
    /// </summary>
    ParticipantUsesLinkedRound,

    /// <summary>The host tried to add a registered user they are not friends with.</summary>
    NotFriends,

    /// <summary>A game type with no registered scoring engine.</summary>
    GameTypeNotSupported
}

/// <summary>Result of a game call: an outcome, and a value when it succeeded.</summary>
public sealed class GameResult<T>
{
    public GameResultStatus Status { get; init; }

    public T? Value { get; init; }

    /// <summary>
    /// Why the call was refused, when the status alone does not say enough — which holes a teebox
    /// is missing, why a field does not suit the game type.
    /// </summary>
    public string? Detail { get; init; }

    public bool IsOk => Status == GameResultStatus.Ok;

    public static GameResult<T> Ok(T value) => new() { Status = GameResultStatus.Ok, Value = value };

    public static GameResult<T> Fail(GameResultStatus status, string? detail = null)
        => new() { Status = status, Detail = detail };
}
