using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Games;

namespace FairwayFinder.Features.Services.Admin;

/// <summary>Row for the admin "all games" grid, with the host so games can be filtered by player.</summary>
public class AdminGameListItemDto
{
    public long GameId { get; set; }
    public GameType GameType { get; set; }
    public GameState State { get; set; }
    public string HostUserId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string HostEmail { get; set; } = string.Empty;
    public DateOnly DatePlayed { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public bool UseNet { get; set; }

    /// <summary>Which holes the game covers, as a label: "18", "Front 9", "Back 9".</summary>
    public string Shape { get; set; } = string.Empty;
}

/// <summary>
/// Everything the detail page needs: the scored game plus the repair options the dialogs offer.
/// </summary>
public class AdminGameDetailDto
{
    public GameStateResponse State { get; set; } = null!;
    public string HostName { get; set; } = string.Empty;
    public string HostEmail { get; set; } = string.Empty;

    /// <summary>The stored snapshot, if the game has been posted. Null otherwise.</summary>
    public string? FinalScoreboardJson { get; set; }

    /// <summary>
    /// The per-hole scoring lines behind the board, keyed by participant then hole. The scorecard
    /// grid renders these — seeing gross, net, and strokes received side by side is what makes a
    /// mistyped handicap obvious.
    /// </summary>
    public IReadOnlyDictionary<long, IReadOnlyDictionary<int, GameHoleLine>> Lines { get; set; }
        = new Dictionary<long, IReadOnlyDictionary<int, GameHoleLine>>();
}

/// <summary>A teebox an admin may move a participant onto.</summary>
public class AdminGameTeeboxOptionDto
{
    public long TeeboxId { get; set; }
    public string TeeboxName { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    /// <summary>False when the teebox lacks holes the game plays — shown but not selectable.</summary>
    public bool CoversGameHoles { get; set; }

    public string Label => IsArchived ? $"{TeeboxName} (archived)" : TeeboxName;
}

/// <summary>A round an admin may link to a participant: their own, on the game's course.</summary>
public class AdminGameRoundOptionDto
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public string TeeboxName { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool IsComplete { get; set; }

    public string Label => $"#{RoundId} · {DatePlayed:d} · {TeeboxName} · {(IsComplete ? Score.ToString() : "in progress")}";
}

/// <summary>
/// What an admin may change on a participant. Deliberately no GameId and no UserId: both are
/// re-resolved server-side, so a repair cannot move a participant between games or hand their
/// line to a different golfer.
/// </summary>
public class RepairParticipantRequest
{
    public int? CourseHandicap { get; set; }
    public long? TeeboxId { get; set; }
    public int? Team { get; set; }
}
