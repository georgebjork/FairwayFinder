using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Games;

/// <summary>
/// Flattens a game into the <see cref="GameScoringContext"/> the engines consume. This is the one
/// place the two score sources converge — a participant's own linked <see cref="Round"/>, or
/// host-entered <see cref="GameHoleScore"/> rows for a guest — so no engine ever learns that a
/// Round exists.
///
/// It reads <c>score</c> straight through the DbContext rather than going via
/// <c>IRoundService</c>, which means the <c>IsComplete</c> gate that (correctly) hides a friend's
/// in-progress round from the friend feed does not have to be relaxed. A game is authorized on
/// "are you a participant", not "are you friends".
/// </summary>
public sealed class GameScoreReader(IDbContextFactory<ApplicationDbContext> dbContextFactory)
{
    /// <summary>One participant's raw material, before it becomes a scoring line.</summary>
    private sealed record ParticipantSource(
        GameParticipant Participant,
        Teebox Teebox,
        IReadOnlyDictionary<int, int> Strokes,
        bool RoundUnavailable,
        bool RoundIsComplete);

    /// <summary>
    /// Whether a participant's linked round could be posted right now. The game never posts it —
    /// completing a game is the host's action, and it must not fire another golfer's stats and
    /// friend notifications as a side effect. This just tells the app when to offer the button.
    /// </summary>
    public readonly record struct RoundPostState(bool IsLinked, bool IsComplete, bool ReadyToPost);

    public sealed record GameReadModel(
        GameScoringContext Context,
        IReadOnlyList<int> HoleNumbers,
        IReadOnlyDictionary<long, int> PlayingHandicaps,
        IReadOnlyDictionary<long, int> HolesEntered,
        IReadOnlyDictionary<long, bool> RoundUnavailable,
        IReadOnlyDictionary<long, RoundPostState> RoundPostStates,
        IReadOnlyDictionary<long, string> TeeboxNames);

    /// <summary>
    /// The hole numbers a game covers, derived from its own shape flags. Fixed at create, so a
    /// player joining later with a nine-hole teebox cannot shrink a match that is already running.
    /// </summary>
    public static IReadOnlyList<int> HoleNumbersFor(Game game)
    {
        if (game.FullRound) return [.. Enumerable.Range(1, 18)];
        if (game.FrontNine) return [.. Enumerable.Range(1, 9)];
        if (game.BackNine) return [.. Enumerable.Range(10, 9)];

        // Shape is validated on create, so this is unreachable in practice.
        return [.. Enumerable.Range(1, 18)];
    }

    public async Task<GameReadModel> ReadAsync(Game game, IReadOnlyList<GameParticipant> participants)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var holeNumbers = HoleNumbersFor(game);
        var sources = new List<ParticipantSource>(participants.Count);

        // Two participants on the same tee should cost one set of queries, not two.
        var teeboxCache = new Dictionary<long, Teebox>();
        var holeCache = new Dictionary<long, IReadOnlyDictionary<int, Hole>>();
        var strokeIndexCache = new Dictionary<long, IReadOnlyDictionary<int, int>>();

        foreach (var participant in participants)
        {
            var (strokes, roundUnavailable, roundIsComplete) = participant.RoundId is { } roundId
                ? await ReadLinkedRoundAsync(dbContext, roundId)
                : (await ReadHostEnteredAsync(dbContext, participant.GameParticipantId), false, false);

            if (!teeboxCache.TryGetValue(participant.TeeboxId, out var teebox))
            {
                teebox = await dbContext.Teeboxes.AsNoTracking()
                    .FirstAsync(t => t.TeeboxId == participant.TeeboxId);
                teeboxCache[participant.TeeboxId] = teebox;
            }

            sources.Add(new ParticipantSource(participant, teebox, strokes, roundUnavailable, roundIsComplete));
        }

        var playingHandicaps = GameHandicapHelper.PlayingHandicaps(
            sources.Select(s => (s.Participant.GameParticipantId, s.Participant.CourseHandicap)),
            game.HandicapAllowancePercent,
            game.StrokesOffLow);

        var lines = new List<GameParticipantLine>(sources.Count);

        foreach (var source in sources)
        {
            var holes = await ResolveHolesAsync(dbContext, source.Teebox, holeCache, strokeIndexCache);

            var ranks = GameHandicapHelper.RankHolesByStrokeIndex(
                holeNumbers.Select(n => (n, holes.TryGetValue(n, out var h) ? h.Handicap : 0)));

            var playingHandicap = playingHandicaps[source.Participant.GameParticipantId];

            var holeLines = holeNumbers.ToDictionary(
                n => n,
                n => new GameHoleLine(
                    HoleNumber: n,
                    Par: holes.TryGetValue(n, out var hole) ? hole.Par : 0,
                    StrokeIndex: holes.TryGetValue(n, out var indexed) ? indexed.Handicap : 0,
                    Strokes: source.Strokes.TryGetValue(n, out var s) ? s : null,
                    StrokesReceived: GameHandicapHelper.StrokesReceived(
                        playingHandicap, ranks[n], holeNumbers.Count)));

            lines.Add(new GameParticipantLine(
                source.Participant.GameParticipantId,
                source.Participant.DisplayName,
                source.Participant.CourseHandicap,
                playingHandicap,
                source.Participant.Team,
                holeLines));
        }

        var rules = new GameRules(
            game.UseNet,
            game.HandicapAllowancePercent,
            game.StrokesOffLow,
            game.SkinsCarryover,
            game.SkinsValue);

        return new GameReadModel(
            new GameScoringContext(game.GameType, rules, holeNumbers, lines),
            holeNumbers,
            playingHandicaps,
            sources.ToDictionary(
                s => s.Participant.GameParticipantId,
                s => s.Strokes.Count(kv => holeNumbers.Contains(kv.Key))),
            sources.ToDictionary(s => s.Participant.GameParticipantId, s => s.RoundUnavailable),
            sources.ToDictionary(s => s.Participant.GameParticipantId, PostStateFor),
            sources.ToDictionary(s => s.Participant.GameParticipantId, s => s.Teebox.TeeboxName));
    }

    /// <summary>
    /// Whether this participant's round could be posted. Judged on the round's <em>own</em> holes,
    /// not the game's: an eighteen-hole round backing a front-nine game is postable once all
    /// eighteen are in, and a match conceded on 15 is postable by neither measure.
    /// </summary>
    private static RoundPostState PostStateFor(ParticipantSource source)
    {
        if (source.Participant.RoundId is null) return new RoundPostState(false, false, false);
        if (source.RoundIsComplete) return new RoundPostState(true, true, false);

        var entered = source.Strokes.Keys.ToHashSet();
        var ready = entered.Count > 0
                    && RoundScoringHelper.MissingHoles(entered, source.Teebox.IsNineHole).Count == 0;

        return new RoundPostState(true, false, ready);
    }

    /// <summary>
    /// A linked round's scores, keyed by hole number. Deliberately ungated on
    /// <c>round.IsComplete</c> — scoring a game as it is played is the entire point.
    /// </summary>
    private static async Task<(IReadOnlyDictionary<int, int> Strokes, bool RoundUnavailable, bool RoundIsComplete)>
        ReadLinkedRoundAsync(ApplicationDbContext dbContext, long roundId)
    {
        // Read the round row itself rather than inferring from "found no scores": a round just
        // started has no scores and is perfectly available.
        var round = await dbContext.Rounds.AsNoTracking()
            .Where(r => r.RoundId == roundId && !r.IsDeleted)
            .Select(r => new { r.IsComplete })
            .FirstOrDefaultAsync();

        if (round is null) return (new Dictionary<int, int>(), true, false);

        var strokes = await dbContext.Scores.AsNoTracking()
            .Where(s => s.RoundId == roundId && !s.IsDeleted)
            .Join(dbContext.Holes.AsNoTracking().Where(h => !h.IsDeleted),
                s => s.HoleId, h => h.HoleId,
                (s, h) => new { h.HoleNumber, s.HoleScore })
            .ToListAsync();

        return (strokes.ToDictionary(x => x.HoleNumber, x => (int)x.HoleScore), false, round.IsComplete);
    }

    private static async Task<IReadOnlyDictionary<int, int>> ReadHostEnteredAsync(
        ApplicationDbContext dbContext, long participantId)
    {
        var strokes = await dbContext.GameHoleScores.AsNoTracking()
            .Where(s => s.GameParticipantId == participantId && !s.IsDeleted)
            .Select(s => new { s.HoleNumber, s.Strokes })
            .ToListAsync();

        return strokes.ToDictionary(x => x.HoleNumber, x => (int)x.Strokes);
    }

    /// <summary>
    /// A teebox's holes, with any unset stroke index borrowed from the newest version of the same
    /// tee lineage that has one.
    /// </summary>
    private static async Task<IReadOnlyDictionary<int, Hole>> ResolveHolesAsync(
        ApplicationDbContext dbContext,
        Teebox teebox,
        Dictionary<long, IReadOnlyDictionary<int, Hole>> holeCache,
        Dictionary<long, IReadOnlyDictionary<int, int>> strokeIndexCache)
    {
        if (holeCache.TryGetValue(teebox.TeeboxId, out var cached)) return cached;

        var holes = await dbContext.Holes.AsNoTracking()
            .Where(h => h.TeeboxId == teebox.TeeboxId && !h.IsDeleted)
            .ToListAsync();

        var byNumber = holes.ToDictionary(h => h.HoleNumber);

        // hole.Handicap is a non-nullable int, so 0 is the only way an unset stroke index can read.
        if (byNumber.Values.Any(h => h.Handicap <= 0))
        {
            if (!strokeIndexCache.TryGetValue(teebox.TeeboxGroupId, out var fallback))
            {
                var lineage = await dbContext.Holes.AsNoTracking()
                    .Where(h => !h.IsDeleted && h.Handicap > 0)
                    .Join(dbContext.Teeboxes.AsNoTracking()
                            .Where(t => !t.IsDeleted && t.TeeboxGroupId == teebox.TeeboxGroupId),
                        h => h.TeeboxId, t => t.TeeboxId,
                        (h, t) => new { h.HoleNumber, h.Handicap, h.TeeboxId })
                    .ToListAsync();

                // The newest version of the lineage wins — the same instinct as
                // StatsCalculator's per-hole fallback, but scoped to one tee rather than
                // borrowing an index from whatever teebox happens to have one.
                fallback = lineage
                    .GroupBy(x => x.HoleNumber)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.TeeboxId).First().Handicap);

                strokeIndexCache[teebox.TeeboxGroupId] = fallback;
            }

            foreach (var hole in byNumber.Values.Where(h => h.Handicap <= 0))
            {
                if (fallback.TryGetValue(hole.HoleNumber, out var borrowed)) hole.Handicap = borrowed;
            }
        }

        holeCache[teebox.TeeboxId] = byNumber;
        return byNumber;
    }
}
