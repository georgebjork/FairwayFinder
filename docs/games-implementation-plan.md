# Games with Friends — live side-game scoring

> **Status: shipped, with corrections.** Match Play and Skins are implemented end to end —
> schema, engines, reader, service, API, admin console, telemetry, push. Stroke Play, Nassau, and
> Stableford are deferred; `GameType` reserves 2, 3, 4 for them.
>
> Review before implementation found defects in the design below. The document is kept for its
> reasoning, but **where it disagrees with the code, the code is right.** The material deviations:
>
> | Area | What changed |
> |---|---|
> | Match play | The result **freezes at the deciding hole**. As drafted, a match won 4 & 3 re-read as "1 UP" once the group played 16–18 out for a skins game. `DecidedOnHole` is now actually set. |
> | The walk | §2's `CompletedHoleNumbers` (a set) and §6's "stop at the first incomplete hole" (a prefix) contradicted each other. Replaced by one `SettledHoleNumbers` prefix, which both engines walk. A gap can no longer let skins carryover leap a hole. |
> | Hole set | Derived from the game's **own** shape flags, fixed at create — not intersected across participants' teeboxes, which let a nine-hole tee joining late shrink an eighteen-hole game. `Game` gained `FullRound`/`FrontNine`/`BackNine`. |
> | Teeboxes | Linking a round **forces** `participant.TeeboxId = round.TeeboxId`; the round's scores join to holes on that teebox. Archived tees are refused when *chosen*, allowed when *inherited* from a round. Course membership and hole coverage are validated. |
> | Snapshot | `FinalScoreboard` is serialized as `GameScoreboard` (the base type) or the polymorphic discriminator is omitted and it cannot be read back. A completed game **serves the snapshot** instead of recomputing — otherwise the column is decorative. |
> | Stroke index | The cited `StatsCalculator` fallback was not a usable precedent (it walks in-memory DTOs and ignores `TeeboxGroupId`). The reader runs a new lineage-scoped query, gated so clean courses pay nothing. |
> | Handicaps | Allowance rounds **away from zero** (.NET's default is banker's, which is not the golf convention); `StrokesReceived` guards a zero hole count; rank ties break on hole number. A game's course handicap is defined as strokes over *the holes that game covers* — nine-hole games are not auto-halved. |
> | Authorization | Adding a registered user requires an existing friendship. Host-entered strokes are refused for a participant scored from a linked round, and permitted for the host or the participant themselves. |
> | ETag | Hashes the **whole serialized response**. The drafted hash missed handicaps and rules, so changing a handicap left a polling phone stale indefinitely. |
> | Naming | `Game.State` (was `Status`) vs `GameResultStatus` — `.Status` now means one thing everywhere. |
> | Plumbing | `LinkRoundAsync` takes a nullable round id so a bad link can be undone. Join codes are generated from an unambiguous alphabet with a collision retry, and looked up filtered on state. `Scoreboard` is nullable while a game is in setup. `FairwayFinder.Games` is registered in **both** `ServiceDefaults` OTel arrays. `DisplayNameHelper` replaced five duplicates, not two. |


## Context

FairwayFinder tracks a single golfer's round in isolation. The hole-by-hole entry flow
(`POST /rounds/start` → `PUT /rounds/{id}/holes/{n}` → `POST /rounds/{id}/complete`) already puts
every stroke on the server as it is played, and `Friendship` already models who plays with whom —
but nothing ties two golfers' rounds together. There is no way to play a match against a friend and
watch it score itself.

This adds **games**: a side contest laid over the rounds people are already entering. A game knows
its participants, where each participant's strokes come from, and which scoring rules apply. The
iOS app polls one endpoint and gets a live scoreboard.

The design has three parts, and the middle one is the point of the exercise:

1. **A small schema** — `game`, `game_participant`, `game_hole_score`.
2. **A scoring engine per game type behind one interface**, so adding Wolf or Bingo-Bango-Bongo
   later is a new class and a DI line, not a change to the service.
3. **One reader** that flattens both score sources — a participant's own linked `Round`, or
   host-entered strokes for a guest — into a single `strokes[hole][participant]` view the engines
   consume. Engines never touch EF.

First cut covers **match play, skins, stroke play, Nassau, and Stableford**, gross or net, with
per-participant course handicaps allocated by the existing `hole.handicap` stroke index.

## Scope decisions (already settled)

| Decision | Choice |
|---|---|
| Score input | Both — link your own `Round`, or the host enters strokes for a guest |
| Game types | Match play, Skins, Stroke play, Nassau, Stableford |
| Handicaps | Per-participant course-handicap number entered by the players; no index, no slope/rating math |
| Live updates | Client polls `GET /api/games/{id}`; ETag keeps polls cheap. No SignalR |

---

## 1. Schema

Three tables, all following house style: explicit `snake_case` via `.ToTable`/`.HasColumnName`,
enums as `int` via `.HasConversion<int>()`, `Restrict` FKs, copy-pasted audit block
(`CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn`/`IsDeleted`), partial unique indexes filtered on
`is_deleted = false`. Config goes inline in `ApplicationDbContext.OnModelCreating`.

### `src/FairwayFinder.Data/Entities/Game.cs`

```csharp
namespace FairwayFinder.Data.Entities;

public class Game
{
    public long GameId { get; set; }
    public GameType GameType { get; set; }
    public long CourseId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public string HostUserId { get; set; } = null!;
    public GameState Status { get; set; }

    /// <summary>Short code a friend types (or deep-links) to join. Unique among live games.</summary>
    public string JoinCode { get; set; } = null!;

    // ── Settings. Deliberately typed columns, not jsonb: the union across all five game
    //    types is six fields, and this codebase has no serialized columns anywhere. ──
    public bool UseNet { get; set; }
    public int HandicapAllowancePercent { get; set; }
    /// <summary>Match-play convention: everyone plays off the low handicap.</summary>
    public bool StrokesOffLow { get; set; }
    public bool SkinsCarryover { get; set; }
    public decimal? SkinsValue { get; set; }
    public bool StablefordModified { get; set; }

    /// <summary>
    /// The scoreboard as it stood when the game was posted, serialized. Rounds stay editable
    /// after they are posted, so without this a settled bet could silently change months later.
    /// </summary>
    public string? FinalScoreboard { get; set; }

    public string CreatedBy { get; set; } = null!;
    public DateOnly CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = null!;
    public DateOnly UpdatedOn { get; set; }
    public bool IsDeleted { get; set; }

    public virtual Course Course { get; set; } = null!;
}

public enum GameType
{
    MatchPlay = 0,
    Skins = 1,
    StrokePlay = 2,
    Nassau = 3,
    Stableford = 4
}

public enum GameState
{
    Setup = 0,
    Active = 1,
    Completed = 2,
    Abandoned = 3
}
```

### `src/FairwayFinder.Data/Entities/GameParticipant.cs`

```csharp
public class GameParticipant
{
    public long GameParticipantId { get; set; }
    public long GameId { get; set; }

    /// <summary>Null for a guest who has no account.</summary>
    public string? UserId { get; set; }

    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Set once this participant links their own round. When set it is the source of truth for
    /// their strokes and <c>game_hole_score</c> is ignored for them.
    /// </summary>
    public long? RoundId { get; set; }

    /// <summary>Their tees — par and stroke index are read from here, not the game.</summary>
    public long TeeboxId { get; set; }

    public int CourseHandicap { get; set; }

    /// <summary>Side for team games. Null for an individual.</summary>
    public int? Team { get; set; }

    /* audit + IsDeleted */

    public virtual Game Game { get; set; } = null!;
    public virtual Teebox Teebox { get; set; } = null!;
}
```

### `src/FairwayFinder.Data/Entities/GameHoleScore.cs`

```csharp
public class GameHoleScore
{
    public long GameHoleScoreId { get; set; }
    public long GameParticipantId { get; set; }
    public int HoleNumber { get; set; }
    public short Strokes { get; set; }

    /* audit + IsDeleted */

    public virtual GameParticipant GameParticipant { get; set; } = null!;
}
```

**Why a separate table rather than a shadow `Round` for guests:** `ix_round_user_id_active` is
keyed on `user_id`, so a guest would need a synthetic one, and those rounds would leak into round
lists and stats. A four-column table avoids all of it.

### `OnModelCreating` blocks

```csharp
        // Game
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.GameId).HasName("game_pkey");
            entity.ToTable("game");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.GameType).HasColumnName("game_type").HasConversion<int>();
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.DatePlayed).HasColumnName("date_played");
            entity.Property(e => e.HostUserId).HasColumnName("host_user_id");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>();
            entity.Property(e => e.JoinCode).HasColumnName("join_code").HasMaxLength(12);
            entity.Property(e => e.UseNet).HasColumnName("use_net").HasDefaultValue(false);
            entity.Property(e => e.HandicapAllowancePercent).HasColumnName("handicap_allowance_percent").HasDefaultValue(100);
            entity.Property(e => e.StrokesOffLow).HasColumnName("strokes_off_low").HasDefaultValue(true);
            entity.Property(e => e.SkinsCarryover).HasColumnName("skins_carryover").HasDefaultValue(true);
            entity.Property(e => e.SkinsValue).HasColumnName("skins_value").HasPrecision(10, 2);
            entity.Property(e => e.StablefordModified).HasColumnName("stableford_modified").HasDefaultValue(false);
            entity.Property(e => e.FinalScoreboard).HasColumnName("final_scoreboard");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");

            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);

            // A join code only has to be unique among games still accepting players.
            entity.HasIndex(e => e.JoinCode)
                .HasDatabaseName("ix_game_join_code_live")
                .IsUnique()
                .HasFilter("status < 2 AND is_deleted = false");

            entity.HasIndex(e => new { e.HostUserId, e.Status }).HasDatabaseName("ix_game_host_status");
        });

        // GameParticipant
        modelBuilder.Entity<GameParticipant>(entity =>
        {
            entity.HasKey(e => e.GameParticipantId).HasName("game_participant_pkey");
            entity.ToTable("game_participant");
            // ...columns...
            entity.HasOne(e => e.Game).WithMany().HasForeignKey(e => e.GameId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teebox).WithMany().HasForeignKey(e => e.TeeboxId).OnDelete(DeleteBehavior.Restrict);

            // A golfer joins a game once. Guests (null user_id) are exempt — several may share a game.
            entity.HasIndex(e => new { e.GameId, e.UserId })
                .HasDatabaseName("ix_game_participant_game_user")
                .IsUnique()
                .HasFilter("user_id IS NOT NULL AND is_deleted = false");

            entity.HasIndex(e => new { e.UserId, e.GameId }).HasDatabaseName("ix_game_participant_user");
        });

        // GameHoleScore
        modelBuilder.Entity<GameHoleScore>(entity =>
        {
            entity.HasKey(e => e.GameHoleScoreId).HasName("game_hole_score_pkey");
            entity.ToTable("game_hole_score");
            // ...columns...
            entity.HasOne(e => e.GameParticipant).WithMany().HasForeignKey(e => e.GameParticipantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Mirrors ix_score_round_id_hole_id: makes the per-hole upsert safe under concurrency.
            entity.HasIndex(e => new { e.GameParticipantId, e.HoleNumber })
                .HasDatabaseName("ix_game_hole_score_participant_hole")
                .IsUnique()
                .HasFilter("is_deleted = false");
        });
```

Plus three `DbSet`s: `Games`, `GameParticipants`, `GameHoleScores`. One migration
(`AddGames`), no data backfill. Applied by the Admin host at startup as usual —
`dotnet ef migrations add AddGames --project src/FairwayFinder.Data --startup-project src/FairwayFinder.Admin`.

---

## 2. The scoring engine pattern

New folder `src/FairwayFinder.Features/Games/`. Everything here is **pure** — no EF, no async, no
DI beyond the engines themselves — following how `StatsCalculator` and `StrokesGainedCalculator`
keep math out of services.

### `Games/IGameScoringEngine.cs`

```csharp
namespace FairwayFinder.Features.Games;

/// <summary>
/// One game's scoring rules. Implementations are pure functions over a
/// <see cref="GameScoringContext"/>: no database, no clock, no state. That is what makes them
/// trivially testable and what lets the same engine score a live game and a finished one.
/// </summary>
public interface IGameScoringEngine
{
    GameType GameType { get; }
    GameScoreboard Score(GameScoringContext context);
}

/// <summary>
/// The typed face of <see cref="IGameScoringEngine"/>. Engines implement this so their own tests
/// and any direct caller get the concrete scoreboard back; the resolver holds the non-generic
/// form so every game type can live in one dictionary.
/// </summary>
public interface IGameScoringEngine<out TScoreboard> : IGameScoringEngine
    where TScoreboard : GameScoreboard
{
    new TScoreboard Score(GameScoringContext context);
}

/// <summary>Bridges the two so an engine only writes <c>Score</c> once.</summary>
public abstract class GameScoringEngine<TScoreboard> : IGameScoringEngine<TScoreboard>
    where TScoreboard : GameScoreboard
{
    public abstract GameType GameType { get; }
    public abstract TScoreboard Score(GameScoringContext context);
    GameScoreboard IGameScoringEngine.Score(GameScoringContext context) => Score(context);
}
```

### `Games/GameScoringContext.cs`

```csharp
/// <summary>
/// Everything an engine needs, already flattened. Strokes have been gathered from whichever
/// source each participant uses, and strokes-received has already been allocated — so no engine
/// reimplements handicap math, and none of them knows a Round exists.
/// </summary>
public sealed record GameScoringContext(
    GameType GameType,
    GameRules Rules,
    IReadOnlyList<int> HoleNumbers,
    IReadOnlyList<GameParticipantLine> Participants)
{
    /// <summary>
    /// Holes every participant has a score for — the only holes a game can settle on. Computed,
    /// not an initialized auto-property: a record's <c>with</c> copies fields rather than re-running
    /// initializers, so a cached version would go stale the moment <see cref="Slice"/> narrowed it.
    /// </summary>
    public IReadOnlyList<int> CompletedHoleNumbers =>
        [.. HoleNumbers.Where(n => Participants.All(p => p.Holes.TryGetValue(n, out var h) && h.Strokes is not null))];

    /// <summary>Narrows the context to a subset of holes. Nassau scores three matches this way.</summary>
    public GameScoringContext Slice(IReadOnlyList<int> holeNumbers) => this with
    {
        HoleNumbers = holeNumbers,
        Participants = [.. Participants.Select(p => p with
        {
            Holes = p.Holes.Where(kv => holeNumbers.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value)
        })]
    };
}

public sealed record GameRules(
    bool UseNet,
    int HandicapAllowancePercent,
    bool StrokesOffLow,
    bool SkinsCarryover,
    decimal? SkinsValue,
    bool StablefordModified);

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
```

### `Games/GameScoreboard.cs`

Polymorphic so the iOS app decodes a tagged union off `gameType`, with a uniform
`Standings` list every game shares for a generic leaderboard row.

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "gameType")]
[JsonDerivedType(typeof(MatchPlayScoreboard), "MatchPlay")]
[JsonDerivedType(typeof(SkinsScoreboard), "Skins")]
[JsonDerivedType(typeof(StrokePlayScoreboard), "StrokePlay")]
[JsonDerivedType(typeof(NassauScoreboard), "Nassau")]
[JsonDerivedType(typeof(StablefordScoreboard), "Stableford")]
public abstract record GameScoreboard
{
    /// <summary>Holes every participant has scored. Not "holes anyone has scored".</summary>
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
    long ParticipantId, string DisplayName, int Position, string Value, bool IsLeader);
```

Per-game shapes:

```csharp
public sealed record MatchPlayScoreboard : GameScoreboard
{
    /// <summary>Null when all square.</summary>
    public required long? LeaderParticipantId { get; init; }
    public required int HolesUp { get; init; }
    public required bool IsDormie { get; init; }
    /// <summary>The hole the match closed out on, if it did.</summary>
    public required int? DecidedOnHole { get; init; }
    /// <summary>"4 &amp; 3", "1 UP", "AS".</summary>
    public required string ResultLine { get; init; }
    public required IReadOnlyList<MatchPlayHole> Holes { get; init; }
}

public sealed record MatchPlayHole(
    int HoleNumber, int Par,
    IReadOnlyList<MatchPlayHoleSide> Sides,
    long? WonByParticipantId, bool IsHalved,
    long? RunningLeaderParticipantId, int RunningHolesUp);

public sealed record MatchPlayHoleSide(
    long ParticipantId, int? Strokes, int? Net, int StrokesReceived);

public sealed record SkinsScoreboard : GameScoreboard
{
    public required int CarriedSkins { get; init; }
    public required IReadOnlyList<SkinsHole> Holes { get; init; }
    public required IReadOnlyList<SkinsTally> Tallies { get; init; }
}
public sealed record SkinsHole(
    int HoleNumber, int Par, long? WonByParticipantId, int SkinsAwarded, int CarriedIn, bool IsTied);
public sealed record SkinsTally(long ParticipantId, string DisplayName, int SkinsWon, decimal? Value);

public sealed record StrokePlayScoreboard : GameScoreboard
{
    public required IReadOnlyList<StrokePlayLine> Lines { get; init; }
}
public sealed record StrokePlayLine(
    long ParticipantId, string DisplayName, int Position,
    int Gross, int Net, int ToPar, int Out, int In, int HolesPlayed);

// The three sub-matches are declared as the concrete type, so they serialize without a
// discriminator — the client already knows what they are from the field name.
public sealed record NassauScoreboard : GameScoreboard
{
    public required MatchPlayScoreboard Front { get; init; }
    public required MatchPlayScoreboard Back { get; init; }
    public required MatchPlayScoreboard Overall { get; init; }
}

public sealed record StablefordScoreboard : GameScoreboard
{
    public required bool IsModified { get; init; }
    public required IReadOnlyList<StablefordLine> Lines { get; init; }
}
public sealed record StablefordLine(
    long ParticipantId, string DisplayName, int Position, int Points, int HolesPlayed);
```

### The five engines

| File | Notes |
|---|---|
| `Games/Engines/StrokePlayScoringEngine.cs` | Simplest. Sum effective scores, rank, split on the turn. Build this first — it exercises the whole contract. |
| `Games/Engines/MatchPlayScoringEngine.cs` | Exactly 2 sides (individuals or teams — a team's hole score is its best ball). Walk completed holes, ±1 per hole won, halve on a tie. Dormie when `HolesUp == HolesRemaining`. Closed out when `HolesUp > HolesRemaining` → `ResultLine` = `"{HolesUp} & {HolesRemaining}"`, `IsDecided` true. |
| `Games/Engines/SkinsScoringEngine.cs` | A hole awards `1 + carriedIn` skins to a sole low effective score; a tie carries when `SkinsCarryover`, otherwise the skins are void. Unplayed holes stop the walk — carryover must not jump a gap. |
| `Games/Engines/StablefordScoringEngine.cs` | Points off each participant's own par (tees can differ). Standard `{-3:0, -2:0, -1:1, 0:2, +1:3, +2:4, +3:5}` net-to-par table; modified `{eagle+:8, eagle:5, birdie:2, par:0, bogey:-1, double+:-3}`. |
| `Games/Engines/NassauScoringEngine.cs` | Composes match play: `Slice(1..9)`, `Slice(10..18)`, and the whole. This is the payoff of the pattern — Nassau is ~30 lines. Requires an 18-hole game. |

### Resolver

```csharp
// Games/GameScoringEngineResolver.cs
public interface IGameScoringEngineResolver
{
    IGameScoringEngine For(GameType gameType);
}

public sealed class GameScoringEngineResolver : IGameScoringEngineResolver
{
    private readonly Dictionary<GameType, IGameScoringEngine> _engines;

    public GameScoringEngineResolver(IEnumerable<IGameScoringEngine> engines)
        => _engines = engines.ToDictionary(e => e.GameType);

    public IGameScoringEngine For(GameType gameType)
        => _engines.TryGetValue(gameType, out var engine)
            ? engine
            : throw new NotSupportedException($"No scoring engine registered for {gameType}.");
}
```

Adding a game type later = one engine class + one `AddSingleton<IGameScoringEngine, X>()` line +
one `GameType` enum value + one `[JsonDerivedType]`. Nothing else changes.

---

## 3. Handicap allocation

New static `src/FairwayFinder.Features/Helpers/GameHandicapHelper.cs` — deliberately separate from
`RoundScoringHelper`, which is about persisting a round's own columns. Pure, fully unit-testable.

```csharp
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
        bool strokesOffLow);

    /// <summary>
    /// Strokes received on one hole. <paramref name="strokeIndexRank"/> is the hole's rank
    /// among the holes the game covers (1 = hardest), not the raw card index — that is what
    /// makes a back-nine game correct, where the raw indexes are 2, 4, 6 … 18.
    /// </summary>
    public static int StrokesReceived(int playingHandicap, int strokeIndexRank, int holeCount);

    /// <summary>
    /// Ranks the game's holes by stroke index. Falls back to hole-number order when a teebox has
    /// no indexes set, so allocation is at least deterministic on imported courses.
    /// </summary>
    public static IReadOnlyDictionary<int, int> RankHolesByStrokeIndex(
        IEnumerable<(int HoleNumber, int StrokeIndex)> holes);
}
```

`StrokesReceived`:

```
if (playingHandicap == 0) return 0;
var magnitude = Math.Abs(playingHandicap);
var baseStrokes = magnitude / holeCount;
var remainder   = magnitude % holeCount;
// Positive: extra strokes go to the hardest holes. Negative (a plus player gives strokes
// back): they come off the easiest, so rank from the other end.
var getsExtra = playingHandicap > 0
    ? strokeIndexRank <= remainder
    : strokeIndexRank > holeCount - remainder;
var strokes = baseStrokes + (getsExtra ? 1 : 0);
return playingHandicap > 0 ? strokes : -strokes;
```

**Stroke index fallback.** `hole.handicap` is `0` on some imported courses. Mirror the existing
approach at `src/FairwayFinder.Features/Helpers/StatsCalculator.cs:704-711`: when the
participant's teebox has an unset index for a hole, borrow the same hole number from another
teebox in the same `TeeboxGroupId` that has one. If nothing does, `RankHolesByStrokeIndex` falls
back to hole-number order. Resolved once in the reader, never inside an engine.

---

## 4. The reader and the service

### `Games/GameScoreReader.cs`

The one place both score sources converge. Given a `Game` and its participants, it:

1. Determines the game's hole set — the hole numbers common to every participant's teebox,
   intersected with the shape the host chose (full / front / back).
2. For each participant: if `RoundId` is set, reads `score` joined to `hole` on that round
   (filtering `!IsDeleted` on both, **not** gating on `round.IsComplete` — that is the entire
   point); otherwise reads `game_hole_score`.
3. Resolves par and stroke index from that participant's own teebox, with the fallback above.
4. Computes playing handicaps and per-hole strokes-received via `GameHandicapHelper`.
5. Returns a `GameScoringContext`.

Because it reads `score` directly through `DbContext`, it never passes through `IRoundService` or
`FriendEndpoints` — so the `IsComplete` gate that (correctly) hides a friend's in-progress round
from the friend feed does not need to be relaxed. Authorization for a game is
"are you a participant", not "are you friends".

### `Games/GameResult.cs`

Same shape as `RoundEntryResult<T>` — Features cannot throw the API's exception types.

```csharp
public enum GameStatus
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
    GameShapeUnsupported
}

public sealed class GameResult<T>
{
    public GameStatus Status { get; init; }
    public T? Value { get; init; }
    /// <summary>Set on <see cref="GameStatus.ParticipantCountInvalid"/> and
    /// <see cref="GameStatus.GameShapeUnsupported"/>: why the game type refused the field.</summary>
    public string? Detail { get; init; }
    public bool IsOk => Status == GameStatus.Ok;

    public static GameResult<T> Ok(T value);
    public static GameResult<T> Fail(GameStatus status, string? detail = null);
}
```

### `Services/GameService.cs` + `Services/Interfaces/IGameService.cs`

`GameService(IDbContextFactory<ApplicationDbContext>, IGameScoringEngineResolver, IFriendService,
IPushNotificationService, ILogger<GameService>)`.

```csharp
Task<GameResult<GameStateResponse>> CreateGameAsync(CreateGameRequest request, string hostUserId);
Task<List<GameSummaryResponse>>     GetMyGamesAsync(string userId, bool activeOnly);
Task<GameResult<GameStateResponse>> GetGameAsync(long gameId, string userId);
Task<GameResult<GameStateResponse>> JoinGameAsync(string joinCode, JoinGameRequest request, string userId);
Task<GameResult<GameStateResponse>> AddParticipantAsync(long gameId, AddParticipantRequest request, string hostUserId);
Task<GameResult<GameStateResponse>> UpdateParticipantAsync(long gameId, long participantId, UpdateParticipantRequest request, string userId);
Task<GameResult<GameStateResponse>> RemoveParticipantAsync(long gameId, long participantId, string hostUserId);
Task<GameResult<GameStateResponse>> LinkRoundAsync(long gameId, LinkRoundRequest request, string userId);
Task<GameResult<GameStateResponse>> UpsertParticipantHoleAsync(long gameId, long participantId, int holeNumber, UpsertGameHoleRequest request, string userId);
Task<GameResult<GameStateResponse>> ClearParticipantHoleAsync(long gameId, long participantId, int holeNumber, string userId);
Task<GameResult<GameStateResponse>> StartGameAsync(long gameId, string hostUserId);
Task<GameResult<GameStateResponse>> CompleteGameAsync(long gameId, string hostUserId);
Task<GameResult<GameStateResponse>> AbandonGameAsync(long gameId, string hostUserId);
```

Every mutating call returns the recomputed `GameStateResponse`, so the app never needs a follow-up
GET after writing a hole — the same courtesy `RoundProgressResponse` gives the round flow.

DTOs in `src/FairwayFinder.Features/Data/GameDtos.cs`:

```csharp
public sealed class GameStateResponse
{
    public long GameId { get; set; }
    public GameType GameType { get; set; }
    public GameState Status { get; set; }
    public long CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public DateOnly DatePlayed { get; set; }
    public string HostUserId { get; set; } = "";
    public string JoinCode { get; set; } = "";
    public GameRules Rules { get; set; } = null!;
    public List<int> HoleNumbers { get; set; } = [];
    public List<GameParticipantResponse> Participants { get; set; } = [];
    public GameScoreboard Scoreboard { get; set; } = null!;
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
    public int PlayingHandicap { get; set; }
    public int? Team { get; set; }
    /// <summary>True when a linked round has since been deleted — the app should prompt to relink.</summary>
    public bool RoundUnavailable { get; set; }
}
```

**Display names.** `BuildDisplayName(firstName, lastName, userName)` is currently duplicated in
`FriendService.cs` and `ProfileService.cs:190`. Lift it to a shared
`Helpers/DisplayNameHelper.cs` and have all three call it rather than adding a fourth copy.

### DI

In `RegisterFeatureServices`, `// Domain services` block:

```csharp
services.AddTransient<IGameService, GameService>();

// Scoring engines are pure and stateless, so one instance each. The resolver indexes them
// by GameType; adding a game type is a class and a line here.
services.AddSingleton<IGameScoringEngine, MatchPlayScoringEngine>();
services.AddSingleton<IGameScoringEngine, SkinsScoringEngine>();
services.AddSingleton<IGameScoringEngine, StrokePlayScoringEngine>();
services.AddSingleton<IGameScoringEngine, NassauScoringEngine>();
services.AddSingleton<IGameScoringEngine, StablefordScoringEngine>();
services.AddSingleton<IGameScoringEngineResolver, GameScoringEngineResolver>();
```

---

## 5. API surface

New `src/FairwayFinder.Api/Endpoints/GameEndpoints.cs`, one group, registered in `Program.cs`
after `MapFriendEndpoints()`.

```csharp
var group = app.MapGroup("/api/games").WithTags("Games").RequireAuthorization();
```

| Verb | Route | Purpose |
|---|---|---|
| POST | `/api/games` | Create. Host becomes the first participant. → 201 |
| GET | `/api/games?activeOnly=true` | My games (host or participant) |
| GET | `/api/games/{gameId:long}` | **The poll endpoint.** Full state + scoreboard. ETag |
| POST | `/api/games/join` | `{ joinCode, roundId?, teeboxId, courseHandicap }` |
| POST | `/api/games/{gameId:long}/participants` | Host adds a friend or a guest |
| PUT | `/api/games/{gameId:long}/participants/{participantId:long}` | Handicap / tees / team / name |
| DELETE | `/api/games/{gameId:long}/participants/{participantId:long}` | Host removes (Setup only) |
| POST | `/api/games/{gameId:long}/round` | `{ roundId }` — link my in-progress round |
| PUT | `/api/games/{gameId:long}/participants/{pid:long}/holes/{n:int:range(1,18)}` | Host-entered strokes |
| DELETE | `/api/games/{gameId:long}/participants/{pid:long}/holes/{n:int:range(1,18)}` | Clear |
| POST | `/api/games/{gameId:long}/start` | Setup → Active. Validates the field for the game type |
| POST | `/api/games/{gameId:long}/complete` | Snapshots the scoreboard, → Completed |
| POST | `/api/games/{gameId:long}/abandon` | → Abandoned |

Shared private `MapGameResult` in the same file, mirroring `MapHoleResult`:

```csharp
private static IResult MapGameResult<T>(GameResult<T> result, long gameId) => result.Status switch
{
    GameStatus.Ok => Results.Ok(result.Value),

    // A non-participant gets 404, not 403 — the same reason FriendEndpoints does it:
    // a 403 confirms the game exists.
    GameStatus.NotParticipant or GameStatus.GameNotFound => throw new NotFoundException("Game", gameId),
    GameStatus.ParticipantNotFound => throw new NotFoundException("GameParticipant", gameId),
    GameStatus.HoleNotInGame       => throw new NotFoundException("Hole", gameId),
    GameStatus.JoinCodeInvalid     => throw new NotFoundException("Game", gameId),

    GameStatus.NotHost => throw new ForbiddenException("Game", gameId),

    GameStatus.GameNotInSetup or GameStatus.GameNotActive or GameStatus.GameAlreadyComplete
        or GameStatus.AlreadyJoined or GameStatus.RoundNotOwned or GameStatus.RoundNotOnGameCourse
        or GameStatus.RoundNotActive or GameStatus.ParticipantCountInvalid
        or GameStatus.GameShapeUnsupported
        => throw new ConflictException(result.Detail ?? $"Game {gameId} cannot accept that right now."),

    _ => throw new NotFoundException("Game", gameId)
};
```

**ETag on the poll endpoint.** Hash the strokes matrix plus status and participant ids into a weak
ETag; honour `If-None-Match` with a 304. That keeps a phone polling every few seconds nearly free
without adding a revision column that the round-write path (which knows nothing about games) would
have to bump.

Validators in `src/FairwayFinder.Api/Validators/GameValidators.cs` — `CreateGameRequestValidator`
(course > 0, valid `GameType`, allowance 1–100, at least one of full/front/back),
`JoinGameRequestValidator`, `AddParticipantRequestValidator` (a participant is either a user id or
a guest display name, not neither), `UpsertGameHoleRequestValidator` (strokes > 0).

Telemetry: add `GamesCreated`, `GamesCompleted`, `GameHolesPosted` counters and a
`game.score` activity name to `Diagnostics/FairwayFinderDiagnostics.cs`.

Push (optional, last): reuse the fire-and-swallow shape of `RoundNotifications` to nudge
participants when a game is created and when it is closed out. Polling stays the source of truth.

---

## 6. Edge cases the implementation must handle

- **A hole nobody has finished.** `Strokes == null` means *not played*, never zero. `HolesPlayed`
  counts only holes **every** participant has scored; engines walk `CompletedHoleNumbers`.
- **Mixed teeboxes.** Par and stroke index come from each participant's own teebox. Stableford and
  net scoring are per-participant; skins gross compares raw strokes, which is the convention.
- **Nine-hole games.** The hole set is derived, and stroke-index *rank* (not raw index) drives
  allocation, so a back nine allocates correctly. Nassau on fewer than 18 holes →
  `GameShapeUnsupported`.
- **Skins across a gap.** Carryover must not leap an unplayed hole; the walk stops at the first
  incomplete hole.
- **Match play field size.** Exactly two sides. Validated on `StartGameAsync`, not at create, so a
  host can build the field first → `ParticipantCountInvalid` with a `Detail`.
- **Leaving mid-game.** Participants can only be removed while `Setup`. Once `Active`, the host
  abandons the game. Simpler and more honest than inventing walkover rules.
- **A linked round is deleted.** The reader finds no live scores; surface
  `RoundUnavailable = true` on that participant rather than silently showing zeros.
- **A linked round is posted.** Nothing changes — the scores stay. A game outlives the round's
  completion, which is exactly right.
- **One round, several games.** Allowed and useful (a match and a skins game on the same round).
  No unique constraint on `round_id`.
- **Concurrency on the guest upsert.** Two devices writing the same guest hole → copy the
  catch-`IsUniqueViolation`-detach-re-read-update pattern from
  `RoundEntryService.UpsertHoleAsync`.
- **In-memory tests.** Wrap transactions in the `BeginTransactionAsync` null-guard the round
  service uses; the in-memory provider has no transactions and silently ignores filtered unique
  indexes.

---

## 7. Build order

Each step compiles and is independently testable.

1. **Schema.** Entities, three `OnModelCreating` blocks, `DbSet`s, one `AddGames` migration.
   Verify: `dotnet build`, then run the Admin host and confirm the migration applies.
2. **`GameHandicapHelper` + `DisplayNameHelper`.** Tests:
   `tests/.../Helpers/GameHandicapHelperTests.cs` — allocation at 0 / 9 / 18 / 27, plus handicaps,
   back-nine rank correctness, strokes-off-low, unset stroke indexes.
3. **Scoring contracts + `StrokePlayScoringEngine`.** Tests prove the contract end-to-end on the
   easiest engine: gross and net totals, partial rounds, ties in position.
4. **`MatchPlayScoringEngine`.** Tests: halved holes, net strokes flipping a hole, dormie, closed
   out 4&3, all square through 18, unplayed holes ignored.
5. **`SkinsScoringEngine`, `StablefordScoringEngine`, `NassauScoringEngine`.** Tests: carryover
   and no-carryover, a gap stopping the walk, both point tables, Nassau's three sub-matches.
6. **`GameScoreReader` + `GameService` + `GameResult<T>` + DI.** Tests in
   `tests/.../GameServiceTests.cs` using `InMemoryDbContextFactory`, seeding the same way
   `RoundEntryServiceTests.CreateAsync` does: create → join → link a round → write holes through
   `RoundEntryService` → assert the scoreboard moves; and host-entered guest strokes.
7. **`GameEndpoints` + validators + `Program.cs`.** Verify by hand against Scalar
   (`dotnet run --project src/FairwayFinder.AppHost/`, dev only).
8. **Telemetry, then the optional APNs nudge.**

## Verification

- `dotnet build FairwayFinder.sln` and `dotnet test` after each step.
- End to end via Aspire (`dotnet run --project src/FairwayFinder.AppHost/`) and the Scalar UI at
  the API's `/scalar`: register two users, friend them, create a match-play game as one, join with
  the other, start a round on each, write holes through `PUT /api/rounds/{id}/holes/{n}`, and poll
  `GET /api/games/{id}` to watch the match move. Then repeat with a guest participant scored
  through `PUT /api/games/{id}/participants/{pid}/holes/{n}`.
- Confirm a non-participant gets 404 on `GET /api/games/{id}`, and that a second poll with
  `If-None-Match` returns 304.
