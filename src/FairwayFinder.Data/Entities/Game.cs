using System.Text.Json.Serialization;

using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

/// <summary>
/// A side contest laid over the rounds people are already entering. A game knows its participants,
/// where each participant's strokes come from, and which scoring rules apply — it never owns
/// strokes itself.
/// </summary>
public class Game : IAuditable
{
    public long GameId { get; set; }

    public GameType GameType { get; set; }

    public long CourseId { get; set; }

    public DateOnly DatePlayed { get; set; }

    public string HostUserId { get; set; } = null!;

    public GameState State { get; set; }

    /// <summary>Short code a friend types (or deep-links) to join. Unique among live games.</summary>
    public string JoinCode { get; set; } = null!;

    // ── Which holes the game covers. Fixed at create, mirroring Round's flags, so a player
    //    joining late cannot move the goalposts by bringing a nine-hole teebox. ──
    public bool FullRound { get; set; }

    public bool FrontNine { get; set; }

    public bool BackNine { get; set; }

    // ── Settings. Deliberately typed columns, not jsonb: the union across the shipped game
    //    types is five fields, and this codebase has no serialized columns anywhere. ──
    public bool UseNet { get; set; }

    public int HandicapAllowancePercent { get; set; }

    /// <summary>Match-play and skins convention: everyone plays off the low handicap.</summary>
    public bool StrokesOffLow { get; set; }

    public bool SkinsCarryover { get; set; }

    public decimal? SkinsValue { get; set; }

    /// <summary>
    /// The scoreboard as it stood when the game was posted, serialized. Rounds stay editable
    /// after they are posted, so without this a settled bet could silently change months later.
    /// Must be written with <c>GameScoreboard</c> as the static type or the polymorphic
    /// discriminator is omitted and it cannot be read back.
    /// </summary>
    public string? FinalScoreboard { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Course Course { get; set; } = null!;
}

/// <summary>
/// Persisted as int. Never renumber: 2, 3, 4 are reserved for StrokePlay, Nassau, and Stableford
/// when those engines ship. Only values with a registered scoring engine are declared, so
/// <c>IsInEnum()</c> on the create request is enough to keep an unscoreable game out of the
/// database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameType
{
    MatchPlay = 0,
    Skins = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameState
{
    Setup = 0,
    Active = 1,
    Completed = 2,
    Abandoned = 3
}
