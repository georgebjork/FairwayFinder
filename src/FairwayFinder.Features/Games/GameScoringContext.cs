using FairwayFinder.Data.Entities;

namespace FairwayFinder.Features.Games;

/// <summary>
/// Everything an engine needs, already flattened. Strokes have been gathered from whichever source
/// each participant uses, and strokes-received has already been allocated — so no engine
/// reimplements handicap math, and none of them knows a <c>Round</c> exists.
/// </summary>
public sealed record GameScoringContext(
    GameType GameType,
    GameRules Rules,
    IReadOnlyList<int> HoleNumbers,
    IReadOnlyList<GameParticipantLine> Participants)
{
    /// <summary>
    /// The longest leading run of <see cref="HoleNumbers"/> on which every participant has a score.
    /// A gap stops it: a hole played after an unscored one is not settled, because the result can
    /// still change when the missing hole comes in. That is what keeps skins carryover honest, and
    /// it is the only hole set engines are allowed to walk.
    ///
    /// Computed, not an initialized auto-property — a record's <c>with</c> copies fields rather
    /// than re-running initializers, so a cached version would go stale.
    /// </summary>
    public IReadOnlyList<int> SettledHoleNumbers =>
        [.. HoleNumbers.TakeWhile(n =>
            Participants.All(p => p.Holes.TryGetValue(n, out var h) && h.Strokes is not null))];
}

public sealed record GameRules(
    bool UseNet,
    int HandicapAllowancePercent,
    bool StrokesOffLow,
    bool SkinsCarryover,
    decimal? SkinsValue);

public sealed record GameParticipantLine(
    long ParticipantId,
    string DisplayName,
    int CourseHandicap,
    int PlayingHandicap,
    int? Team,
    IReadOnlyDictionary<int, GameHoleLine> Holes);

/// <summary>One participant's hole. <c>Strokes</c> is null until it has been played.</summary>
public readonly record struct GameHoleLine(
    int HoleNumber,
    int Par,
    int StrokeIndex,
    int? Strokes,
    int StrokesReceived)
{
    public int? Net => Strokes is null ? null : Strokes.Value - StrokesReceived;

    /// <summary>The score the game is settled on: net when the game plays net, gross otherwise.</summary>
    public int? Effective(bool useNet) => useNet ? Net : Strokes;
}
