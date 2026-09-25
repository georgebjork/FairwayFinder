using System.Text.Json.Serialization;

namespace FairwayFinder.Features.Games;

/// <summary>
/// A scored game, as the app sees it. Polymorphic so the client decodes a tagged union off
/// <c>gameType</c>, with a uniform <see cref="Standings"/> list every game shares for a generic
/// leaderboard row.
///
/// Only shipped game types are listed. Serialize with <c>GameScoreboard</c> as the static type or
/// the discriminator is omitted and the payload cannot be read back — which matters most for the
/// snapshot written into <c>game.final_scoreboard</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "gameType")]
[JsonDerivedType(typeof(MatchPlayScoreboard), "MatchPlay")]
[JsonDerivedType(typeof(SkinsScoreboard), "Skins")]
public abstract record GameScoreboard
{
    /// <summary>Holes every participant has scored, in an unbroken run. Not "holes anyone scored".</summary>
    public required int HolesPlayed { get; init; }

    public required int HolesRemaining { get; init; }

    /// <summary>True once the result cannot change — closed out, or every hole is in.</summary>
    public required bool IsDecided { get; init; }

    /// <summary>One line for the app header: "Dale 2 UP thru 14", "Sam leads by 3".</summary>
    public required string Summary { get; init; }

    /// <summary>Type-agnostic leaderboard rows, so a generic list renders for any game.</summary>
    public required IReadOnlyList<GameStanding> Standings { get; init; }
}

public sealed record GameStanding(
    long ParticipantId,
    string DisplayName,
    int Position,
    string Value,
    bool IsLeader);

// ── Match play ──

public sealed record MatchPlayScoreboard : GameScoreboard
{
    /// <summary>Null when all square.</summary>
    public required long? LeaderParticipantId { get; init; }

    /// <summary>
    /// Frozen at the deciding hole once the match is over, so playing the remaining holes out
    /// for a side bet cannot rewrite a settled result.
    /// </summary>
    public required int HolesUp { get; init; }

    public required bool IsDormie { get; init; }

    /// <summary>The hole the match closed out on, if it did.</summary>
    public required int? DecidedOnHole { get; init; }

    /// <summary>"4 &amp; 3", "1 UP", "AS".</summary>
    public required string ResultLine { get; init; }

    /// <summary>Every settled hole, including any played after the match was decided.</summary>
    public required IReadOnlyList<MatchPlayHole> Holes { get; init; }
}

public sealed record MatchPlayHole(
    int HoleNumber,
    int Par,
    IReadOnlyList<MatchPlayHoleSide> Sides,
    long? WonBySideParticipantId,
    bool IsHalved,
    long? RunningLeaderParticipantId,
    int RunningHolesUp);

/// <summary>
/// One side's showing on a hole. Carries every member id, not just the side key, so a four-ball
/// card can show who actually made the number.
/// </summary>
public sealed record MatchPlayHoleSide(
    long SideKeyParticipantId,
    IReadOnlyList<long> ParticipantIds,
    string DisplayName,
    int? Strokes,
    int? Net,
    int StrokesReceived);

// ── Skins ──

public sealed record SkinsScoreboard : GameScoreboard
{
    /// <summary>Skins riding on the next hole because the last one tied.</summary>
    public required int CarriedSkins { get; init; }

    public required IReadOnlyList<SkinsHole> Holes { get; init; }

    public required IReadOnlyList<SkinsTally> Tallies { get; init; }
}

public sealed record SkinsHole(
    int HoleNumber,
    int Par,
    long? WonByParticipantId,
    int SkinsAwarded,
    int CarriedIn,
    bool IsTied);

public sealed record SkinsTally(
    long ParticipantId,
    string DisplayName,
    int SkinsWon,
    decimal? Value);
