using FairwayFinder.Features.Data;
using FairwayFinder.Features.Games;

namespace FairwayFinder.Admin.Components.Pages.Games.Components;

/// <summary>
/// What the game scorecard says about each hole beyond the raw numbers: which sides are playing,
/// whose ball won the hole, and where the game stands after it. Every scoreboard type is reduced
/// to this one shape so <see cref="GameScorecardGrid"/> renders all games the same way.
///
/// A new game type adds one case to <see cref="From"/>. Until it does, its card still shows every
/// score and the sides, just no hole winners.
/// </summary>
public sealed class GameCardModel
{
    private readonly Dictionary<long, GameCardSide> _sideByParticipant;

    private GameCardModel(
        IReadOnlyList<GameCardSide> sides,
        IReadOnlyDictionary<int, GameCardHole> holes,
        string resultLabel,
        string? standingLabel)
    {
        Sides = sides;
        Holes = holes;
        ResultLabel = resultLabel;
        StandingLabel = standingLabel;
        _sideByParticipant = sides
            .SelectMany(s => s.ParticipantIds.Select(id => (id, s)))
            .ToDictionary(x => x.id, x => x.s);
    }

    /// <summary>Empty for an every-player-for-themselves game like skins: rows get no side colour.</summary>
    public IReadOnlyList<GameCardSide> Sides { get; }

    /// <summary>Settled holes only. A hole missing here has not been decided yet.</summary>
    public IReadOnlyDictionary<int, GameCardHole> Holes { get; }

    /// <summary>Row heading for <see cref="GameCardHole.Result"/>.</summary>
    public string ResultLabel { get; }

    /// <summary>Row heading for <see cref="GameCardHole.Standing"/>. Null hides the row.</summary>
    public string? StandingLabel { get; }

    public GameCardSide? SideOf(long participantId) => _sideByParticipant.GetValueOrDefault(participantId);

    public GameCardHole? Hole(int holeNumber) => Holes.GetValueOrDefault(holeNumber);

    public static GameCardModel From(
        GameStateResponse state,
        IReadOnlyDictionary<long, IReadOnlyDictionary<int, GameHoleLine>> lines)
    {
        var sides = SidesFor(state);

        return state.Scoreboard switch
        {
            MatchPlayScoreboard board => MatchPlay(board, sides, state.Rules.UseNet, lines),
            SkinsScoreboard board => Skins(board, sides, state),
            HighLowScoreboard board => HighLow(board, sides),
            _ => new GameCardModel(sides, new Dictionary<int, GameCardHole>(), "Result", null)
        };
    }

    // ── Sides ──

    /// <summary>
    /// Mirrors <see cref="GameSides.Resolve"/>: teams when the field is teamed, otherwise one side
    /// per golfer. Skins has no sides worth colouring, so it gets none.
    /// </summary>
    private static List<GameCardSide> SidesFor(GameStateResponse state)
    {
        if (state.Scoreboard is SkinsScoreboard) return [];

        var field = state.Participants;
        if (field.Count > 0 && field.All(p => p.Team is not null))
        {
            return
            [
                .. field
                    .GroupBy(p => p.Team!.Value)
                    .OrderBy(g => g.Key)
                    .Select((g, i) => new GameCardSide(
                        i,
                        $"T{g.Key}",
                        $"Team {g.Key} ({string.Join(" / ", g.Select(p => p.DisplayName))})",
                        [.. g.Select(p => p.ParticipantId)]))
            ];
        }

        return
        [
            .. field.Select((p, i) => new GameCardSide(
                i, ShortName(p.DisplayName), p.DisplayName, [p.ParticipantId]))
        ];
    }

    // ── Match play ──

    private static GameCardModel MatchPlay(
        MatchPlayScoreboard board,
        List<GameCardSide> sides,
        bool useNet,
        IReadOnlyDictionary<long, IReadOnlyDictionary<int, GameHoleLine>> lines)
    {
        var holes = new Dictionary<int, GameCardHole>();

        foreach (var hole in board.Holes)
        {
            var winner = hole.WonBySideParticipantId is { } key ? Find(sides, key) : null;

            // In a four-ball only the ball that counted won the hole, so only that cell lights up.
            var awards = new Dictionary<long, string?>();
            if (winner is not null)
            {
                foreach (var id in BestBallMembers(winner, hole.HoleNumber, useNet, lines)) awards[id] = null;
            }

            var result = winner is null
                ? new GameCardChip("½", $"Hole {hole.HoleNumber} halved", null)
                : new GameCardChip(winner.Label, $"{winner.DisplayName} won hole {hole.HoleNumber}", winner.Index);

            var leader = hole.RunningLeaderParticipantId is { } l ? Find(sides, l) : null;
            var standing = leader is null
                ? new GameCardChip("AS", $"All square after {hole.HoleNumber}", null)
                : new GameCardChip($"{hole.RunningHolesUp} UP",
                    $"{leader.DisplayName} {hole.RunningHolesUp} UP after {hole.HoleNumber}", leader.Index);

            var afterDecided = board.DecidedOnHole is { } decided && hole.HoleNumber > decided;

            holes[hole.HoleNumber] = new GameCardHole(hole.HoleNumber, awards, result, standing, afterDecided);
        }

        return new GameCardModel(sides, holes, "Hole", "Match");
    }

    /// <summary>The members of a side whose effective score was the side's best on this hole.</summary>
    private static IEnumerable<long> BestBallMembers(
        GameCardSide side,
        int holeNumber,
        bool useNet,
        IReadOnlyDictionary<long, IReadOnlyDictionary<int, GameHoleLine>> lines)
    {
        var scores = side.ParticipantIds
            .Select(id => (id, Score: lines.TryGetValue(id, out var holes) && holes.TryGetValue(holeNumber, out var line)
                ? line.Effective(useNet)
                : null))
            .Where(x => x.Score is not null)
            .ToList();

        if (scores.Count == 0) return side.ParticipantIds;

        var best = scores.Min(x => x.Score);
        return scores.Where(x => x.Score == best).Select(x => x.id);
    }

    // ── Skins ──

    private static GameCardModel Skins(SkinsScoreboard board, List<GameCardSide> sides, GameStateResponse state)
    {
        var names = state.Participants.ToDictionary(p => p.ParticipantId, p => p.DisplayName);
        var holes = new Dictionary<int, GameCardHole>();

        foreach (var hole in board.Holes)
        {
            var awards = new Dictionary<long, string?>();
            GameCardChip result;

            if (hole.WonByParticipantId is { } winnerId)
            {
                var name = names.GetValueOrDefault(winnerId, "?");
                var multiple = hole.SkinsAwarded > 1 ? $" ×{hole.SkinsAwarded}" : "";
                awards[winnerId] = hole.SkinsAwarded > 1 ? $"×{hole.SkinsAwarded}" : null;

                result = new GameCardChip(
                    ShortName(name) + multiple,
                    $"{name} won {Plural(hole.SkinsAwarded, "skin")} on {hole.HoleNumber}",
                    null,
                    IsWin: true);
            }
            else if (state.Rules.SkinsCarryover)
            {
                result = new GameCardChip("Carry",
                    $"Tied — {Plural(hole.CarriedIn + 1, "skin")} carry to the next hole", null);
            }
            else
            {
                result = new GameCardChip("Push", "Tied — the skin is void", null);
            }

            holes[hole.HoleNumber] = new GameCardHole(hole.HoleNumber, awards, result, null, AfterDecided: false);
        }

        return new GameCardModel(sides, holes, "Skin", null);
    }

    // ── High-Low ──

    private static GameCardModel HighLow(HighLowScoreboard board, List<GameCardSide> sides)
    {
        var holes = new Dictionary<int, GameCardHole>();

        foreach (var hole in board.Holes)
        {
            var awards = new Dictionary<long, string?>();

            // Tag the golfer who made each winning number: L for the low ball, H for the high.
            Award(hole.LowWonBySideParticipantId, s => s.LowParticipantId, "L");
            Award(hole.HighWonBySideParticipantId, s => s.HighParticipantId, "H");

            var lowSide = hole.LowWonBySideParticipantId is { } lo ? Find(sides, lo) : null;
            var highSide = hole.HighWonBySideParticipantId is { } hi ? Find(sides, hi) : null;
            var title = $"Hole {hole.HoleNumber} — low: {lowSide?.DisplayName ?? "tied"} · high: {highSide?.DisplayName ?? "tied"}";

            var top = hole.Sides.MaxBy(s => s.Points);
            var topSide = top is null ? null : Find(sides, top.SideKeyParticipantId);
            var split = hole.Sides.Count == 2 && hole.Sides[0].Points == hole.Sides[1].Points;

            var result = split || topSide is null
                ? new GameCardChip(top?.Points > 0 ? "1–1" : "½", title, null)
                : new GameCardChip($"{topSide.Label} +{top!.Points}", title, topSide.Index);

            var leader = hole.RunningLeaderParticipantId is { } l ? Find(sides, l) : null;
            var standing = leader is null
                ? new GameCardChip("AS", $"Level after {hole.HoleNumber}", null)
                : new GameCardChip($"+{hole.RunningMargin}",
                    $"{leader.DisplayName} lead by {hole.RunningMargin} after {hole.HoleNumber}", leader.Index);

            holes[hole.HoleNumber] = new GameCardHole(hole.HoleNumber, awards, result, standing, AfterDecided: false);

            void Award(long? sideKey, Func<HighLowHoleSide, long?> madeBy, string tag)
            {
                var side = hole.Sides.FirstOrDefault(s => s.SideKeyParticipantId == sideKey);
                if (side is null || madeBy(side) is not { } id) return;

                awards[id] = awards.TryGetValue(id, out var existing) && existing is not null ? existing + tag : tag;
            }
        }

        return new GameCardModel(sides, holes, "Points", "Match");
    }

    // ── Helpers ──

    /// <summary>
    /// Scoreboards name a side by its key participant. Match on membership rather than order so a
    /// side resolves no matter which member the engine chose as its key.
    /// </summary>
    private static GameCardSide? Find(IEnumerable<GameCardSide> sides, long participantId)
        => sides.FirstOrDefault(s => s.ParticipantIds.Contains(participantId));

    private static string ShortName(string displayName)
    {
        var first = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? displayName;
        return first.Length <= 8 ? first : first[..7] + "…";
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}

/// <summary>A competing side. <see cref="Index"/> picks its colour on the card.</summary>
public sealed record GameCardSide(int Index, string Label, string DisplayName, IReadOnlyList<long> ParticipantIds);

/// <summary>
/// One settled hole. <see cref="Awards"/> lights up the cells that won something, keyed by
/// participant, with an optional tag to say what ("L", "H", "×3"). Null tag means a plain win.
/// </summary>
public sealed record GameCardHole(
    int HoleNumber,
    IReadOnlyDictionary<long, string?> Awards,
    GameCardChip Result,
    GameCardChip? Standing,
    bool AfterDecided);

/// <summary>
/// A short label under a hole. Coloured by <see cref="SideIndex"/> when a side owns it, by the
/// success colour when <see cref="IsWin"/> is set without a side, and neutral otherwise.
/// </summary>
public sealed record GameCardChip(string Text, string Title, int? SideIndex, bool IsWin = false);
