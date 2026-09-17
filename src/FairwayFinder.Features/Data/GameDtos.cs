using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Games;

namespace FairwayFinder.Features.Data;

// ── Requests ──

public sealed class CreateGameRequest
{
    public GameType GameType { get; set; }
    public long CourseId { get; set; }
    public long TeeboxId { get; set; }
    public DateOnly DatePlayed { get; set; }

    public bool FullRound { get; set; } = true;
    public bool FrontNine { get; set; }
    public bool BackNine { get; set; }

    /// <summary>
    /// The host's strokes over the holes this game covers — a nine-hole game wants the nine-hole
    /// number, not an eighteen-hole course handicap.
    /// </summary>
    public int CourseHandicap { get; set; }

    /// <summary>Optional: link an already-started round instead of being scored by hand.</summary>
    public long? RoundId { get; set; }

    public int? Team { get; set; }

    public bool UseNet { get; set; }
    public int HandicapAllowancePercent { get; set; } = 100;
    public bool StrokesOffLow { get; set; } = true;
    public bool SkinsCarryover { get; set; } = true;
    public decimal? SkinsValue { get; set; }
}

public sealed class JoinGameRequest
{
    public string JoinCode { get; set; } = "";
    public long TeeboxId { get; set; }

    /// <summary>Strokes over the holes this game covers.</summary>
    public int CourseHandicap { get; set; }

    public long? RoundId { get; set; }
    public int? Team { get; set; }
}

public sealed class AddParticipantRequest
{
    /// <summary>A registered golfer the host is friends with. Null for a guest.</summary>
    public string? UserId { get; set; }

    /// <summary>Required for a guest; ignored for a registered golfer, whose name is snapshotted.</summary>
    public string? DisplayName { get; set; }

    public long TeeboxId { get; set; }

    /// <summary>Strokes over the holes this game covers.</summary>
    public int CourseHandicap { get; set; }

    public int? Team { get; set; }
}

public sealed class UpdateParticipantRequest
{
    public int? CourseHandicap { get; set; }
    public long? TeeboxId { get; set; }
    public int? Team { get; set; }
    public string? DisplayName { get; set; }
}

public sealed class LinkRoundRequest
{
    /// <summary>Null unlinks, handing scoring back to host-entered strokes.</summary>
    public long? RoundId { get; set; }
}

public sealed class UpsertGameHoleRequest
{
    public short Strokes { get; set; }
}

// ── Responses ──

public sealed class GameStateResponse
{
    public long GameId { get; set; }
    public GameType GameType { get; set; }
    public GameState State { get; set; }
    public long CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public DateOnly DatePlayed { get; set; }
    public string HostUserId { get; set; } = "";
    public string JoinCode { get; set; } = "";
    public GameRules Rules { get; set; } = null!;
    public List<int> HoleNumbers { get; set; } = [];
    public List<GameParticipantResponse> Participants { get; set; } = [];

    /// <summary>
    /// Null while the game is still in setup: the field may not be valid for the game type yet,
    /// and there is nothing to score.
    /// </summary>
    public GameScoreboard? Scoreboard { get; set; }
}

public sealed class GameParticipantResponse
{
    public long ParticipantId { get; set; }
    public string? UserId { get; set; }
    public Guid? PublicIdentifier { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsGuest { get; set; }
    public bool IsHost { get; set; }
    public long? RoundId { get; set; }
    public long TeeboxId { get; set; }
    public string TeeboxName { get; set; } = "";
    public int CourseHandicap { get; set; }

    /// <summary>After the allowance and any strokes-off-low adjustment. What they actually get.</summary>
    public int PlayingHandicap { get; set; }

    public int? Team { get; set; }

    /// <summary>How many of the game's holes this participant has a score for.</summary>
    public int HolesEntered { get; set; }

    /// <summary>True when a linked round has since been deleted — the app should prompt to relink.</summary>
    public bool RoundUnavailable { get; set; }
}

/// <summary>
/// A game in a list. Deliberately carries no scoreboard: scoring every game in a list would run
/// the reader once per row.
/// </summary>
public sealed class GameSummaryResponse
{
    public long GameId { get; set; }
    public GameType GameType { get; set; }
    public GameState State { get; set; }
    public long CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public DateOnly DatePlayed { get; set; }
    public string JoinCode { get; set; } = "";
    public bool IsHost { get; set; }
    public int ParticipantCount { get; set; }
}
