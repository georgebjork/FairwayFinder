using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Games;

namespace FairwayFinder.Features.Tests.Helpers;

/// <summary>
/// Builders for engine tests. Engines are pure functions over a <see cref="GameScoringContext"/>,
/// so their tests need no database at all — just a hand-built context with the holes the test
/// cares about.
/// </summary>
public static class GameScoringTestData
{
    /// <summary>Par for holes 1-18: a standard 36/36, par 72. Same card the round tests use.</summary>
    public static readonly int[] Pars = [4, 5, 3, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 4, 5, 4];

    public static GameRules Rules(
        bool useNet = false,
        int allowancePercent = 100,
        bool strokesOffLow = true,
        bool skinsCarryover = true,
        decimal? skinsValue = null)
        => new(useNet, allowancePercent, strokesOffLow, skinsCarryover, skinsValue);

    /// <summary>
    /// One participant. <paramref name="scores"/> is keyed by hole number; a hole absent from it
    /// (or mapped to null) has not been played.
    /// </summary>
    public static GameParticipantLine Line(
        long participantId,
        IReadOnlyDictionary<int, int?> scores,
        string? displayName = null,
        int playingHandicap = 0,
        int? team = null,
        IReadOnlyDictionary<int, int>? strokesReceived = null,
        IReadOnlyDictionary<int, int>? pars = null)
    {
        var holes = scores.ToDictionary(
            kv => kv.Key,
            kv => new GameHoleLine(
                HoleNumber: kv.Key,
                Par: pars is not null && pars.TryGetValue(kv.Key, out var par) ? par : Pars[kv.Key - 1],
                StrokeIndex: kv.Key,
                Strokes: kv.Value,
                StrokesReceived: strokesReceived is not null && strokesReceived.TryGetValue(kv.Key, out var s) ? s : 0));

        return new GameParticipantLine(
            participantId,
            displayName ?? $"Player {participantId}",
            CourseHandicap: playingHandicap,
            playingHandicap,
            team,
            holes);
    }

    public static GameScoringContext Context(
        GameType gameType,
        IReadOnlyList<GameParticipantLine> participants,
        int holeCount = 18,
        GameRules? rules = null,
        IReadOnlyList<int>? holeNumbers = null)
        => new(
            gameType,
            rules ?? Rules(),
            holeNumbers ?? [.. Enumerable.Range(1, holeCount)],
            participants);

    /// <summary>Every hole 1..<paramref name="through"/> at the same score.</summary>
    public static Dictionary<int, int?> Flat(int score, int through = 18)
        => Enumerable.Range(1, through).ToDictionary(n => n, _ => (int?)score);

    /// <summary>Every hole at par.</summary>
    public static Dictionary<int, int?> AtPar(int through = 18)
        => Enumerable.Range(1, through).ToDictionary(n => n, n => (int?)Pars[n - 1]);
}
