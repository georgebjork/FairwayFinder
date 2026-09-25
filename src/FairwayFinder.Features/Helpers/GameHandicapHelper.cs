namespace FairwayFinder.Features.Helpers;

/// <summary>
/// Handicap allocation for games. Deliberately separate from <see cref="RoundScoringHelper"/>,
/// which is about persisting a round's own columns and carries no handicap logic at all.
///
/// Pure and fully unit-testable — no EF, no clock. Everything here works in terms of a hole's
/// <em>rank</em> among the holes the game covers rather than its raw card index, which is what
/// makes a back-nine game correct: the raw indexes there are 2, 4, 6 … 18.
/// </summary>
public static class GameHandicapHelper
{
    /// <summary>
    /// Playing handicaps for a field. Applies the allowance, then — when
    /// <paramref name="strokesOffLow"/> — subtracts the lowest so the best player plays off
    /// scratch, which is the match-play and skins convention.
    /// </summary>
    public static IReadOnlyDictionary<long, int> PlayingHandicaps(
        IEnumerable<(long ParticipantId, int CourseHandicap)> field,
        int allowancePercent,
        bool strokesOffLow)
    {
        // Golf rounds half away from zero; .NET's default is banker's rounding, which would turn
        // a 90% allowance off 25 into 22 rather than 23.
        var allowed = field.ToDictionary(
            p => p.ParticipantId,
            p => (int)Math.Round(p.CourseHandicap * allowancePercent / 100.0, MidpointRounding.AwayFromZero));

        if (!strokesOffLow || allowed.Count == 0) return allowed;

        var lowest = allowed.Values.Min();
        return allowed.ToDictionary(kv => kv.Key, kv => kv.Value - lowest);
    }

    /// <summary>
    /// Strokes received on one hole. <paramref name="strokeIndexRank"/> is the hole's rank among
    /// the holes the game covers (1 = hardest), not the raw card index.
    /// </summary>
    public static int StrokesReceived(int playingHandicap, int strokeIndexRank, int holeCount)
    {
        if (playingHandicap == 0 || holeCount <= 0) return 0;

        var magnitude = Math.Abs(playingHandicap);
        var baseStrokes = magnitude / holeCount;
        var remainder = magnitude % holeCount;

        // Positive: the extra strokes go to the hardest holes. Negative (a plus player gives
        // strokes back): they come off the easiest, so rank from the other end.
        var getsExtra = playingHandicap > 0
            ? strokeIndexRank <= remainder
            : strokeIndexRank > holeCount - remainder;

        var strokes = baseStrokes + (getsExtra ? 1 : 0);
        return playingHandicap > 0 ? strokes : -strokes;
    }

    /// <summary>
    /// Ranks the game's holes by stroke index, 1 = hardest. Holes with no index set (<c>0</c>,
    /// which is how an unset index reads on an imported course) sort to the end, and ties break on
    /// hole number — so allocation is deterministic whatever the course data looks like.
    /// </summary>
    public static IReadOnlyDictionary<int, int> RankHolesByStrokeIndex(
        IEnumerable<(int HoleNumber, int StrokeIndex)> holes)
    {
        var ranked = holes
            .OrderBy(h => h.StrokeIndex <= 0 ? int.MaxValue : h.StrokeIndex)
            .ThenBy(h => h.HoleNumber)
            .Select((h, i) => (h.HoleNumber, Rank: i + 1));

        return ranked.ToDictionary(x => x.HoleNumber, x => x.Rank);
    }
}
