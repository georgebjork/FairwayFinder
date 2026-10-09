---
name: new-game-type
description: Implement a new golf game type (Nassau, Stableford, Wolf, Bingo Bango Bongo, a new team format...) end to end — enum, scoring engine, scoreboard contract, field validation, rule options, admin scorecard, labels, and tests. Use whenever adding a game the API should score, or changing how an existing game is scored.
argument-hint: "[GameName and a one-line description of its rules]"
---

Add a new game type: $ARGUMENTS

Games are a side contest laid over rounds people are already entering. A game never owns strokes:
`GameScoreReader` flattens every participant's strokes and strokes-received into a
`GameScoringContext`, and a **pure scoring engine** turns that into a **scoreboard**. Everything
else — the API, the posted snapshot, the admin card, the iOS app — reads the scoreboard. Get the
engine and scoreboard right and the rest is wiring.

Read the closest existing engine before writing anything:

| Engine | Shape | Read it for |
|---|---|---|
| `MatchPlayScoringEngine` | two sides, hole won/lost/halved | best ball, running state, freezing a result once decided |
| `SkinsScoringEngine` | individual, per-hole winner | carryover, money values, competition ranking |
| `HighLowScoringEngine` | two equal teams, two points a hole | multiple points per hole, "who made the number" attribution |

All under `src/FairwayFinder.Features/Games/`. The `high low game` commit (`git show 95cfe83`) is a
complete worked example of every step below.

## Step 0 — Pin the rules down before coding

Golf games have house rules. Settle these with the user first (use `AskUserQuestion` for the ones
that genuinely vary); the answers become the engine's doc comment and its tests:

- **Field**: individuals, fixed teams, or both? How many players/sides, and must teams be equal?
- **Unit of scoring**: per hole (points, holes won, skins) or aggregate (total strokes, points)?
- **Ties**: halved, carried, void, split?
- **Net vs gross**: does the game honour `UseNet` / `HandicapAllowancePercent` / `StrokesOffLow`
  like the others, or ignore one of them?
- **Decided early?** Can it be closed out, and if so does the result freeze (match play) or keep
  moving (high-low)?
- **Options** the host picks at create (presses, point values, carryover). Each is a new column — keep
  the list short.
- **Does it fit the model?** Teams are fixed per participant for the whole game (`GameParticipant.Team`).
  Rotating partners (Wolf, Six-Six-Six) or per-hole events not derivable from strokes (Bingo Bango
  Bongo's "first on the green") do **not** fit and need a schema design conversation first. Say so
  rather than forcing it.

## Step 1 — Enum (`src/FairwayFinder.Data/Entities/Game.cs`)

- Persisted as int. **Never renumber.** `2`, `3`, `4` are reserved for StrokePlay, Nassau, Stableford;
  use the reserved number for those, otherwise the next free value (HighLow is `5`). Update the
  reservation comment.
- Only declare a value once its engine ships — `IsInEnum()` on `CreateGameRequest` is what keeps an
  unscoreable game out of the database.

## Step 2 — Scoreboard contract (`Games/GameScoreboard.cs`)

- A `sealed record {Name}Scoreboard : GameScoreboard`, plus per-hole and per-side records, in a
  `// ── {Name} ──` section.
- Register it: `[JsonDerivedType(typeof({Name}Scoreboard), "{EnumName}")]`. The discriminator **must
  equal the enum name** — the iOS app and the posted snapshot both key off it.
- The base fields (`HolesPlayed`, `HolesRemaining`, `IsDecided`, `Summary`, `Standings`) are what an
  older client falls back to, so make `Summary` a complete sentence ("Dale 2 UP thru 14") and
  `Standings.Value` a short readable string ("9 pts", "3 skins").
- **Every settled hole goes in `Holes`**, with enough to render the card without re-deriving the
  rules: the winner (by side key), each side's counting score, and **which participant made it**
  (`LowParticipantId`-style fields). The admin card and iOS grid both highlight the golfer whose
  ball counted; if the payload doesn't say, clients have to guess.
- Name sides by `SideKeyParticipantId` (the side's first member, `GameSide.KeyParticipantId`) and
  carry `ParticipantIds` too.
- This is a wire contract with a shipped app. Add fields freely; never rename or retype one.

## Step 3 — Engine (`Games/Engines/{Name}ScoringEngine.cs`)

```csharp
public sealed class {Name}ScoringEngine : GameScoringEngine<{Name}Scoreboard>
{
    public override GameType GameType => GameType.{Name};

    public override {Name}Scoreboard Score(GameScoringContext context) { ... }
}
```

Rules every engine follows:

- **Pure.** No database, no clock, no DI. Same input, same board — that's what lets one engine score
  a live game and re-score a posted one.
- **Walk only `context.SettledHoleNumbers`**: the unbroken run of holes everyone has scored. Never
  `HoleNumbers`, never "holes anyone scored" — a gap means the result can still change.
- Use `hole.Effective(useNet)` for the score the game settles on. Strokes-received is already
  allocated; never reimplement handicap math in an engine.
- Build sides with `GameSides.Resolve`, and team scores with `GameSides.BestBall` / `WorstBall`. Add
  any new side helper there (Nassau will share match play's), not inside your engine.
- If the field is unplayable (wrong side count), return an `Empty(...)` board with an explanatory
  `Summary` instead of throwing. Start-time validation normally prevents it.
- Par for the card comes from the first participant's line (mixed tees have no single par).
- Standings use standard competition ranking (ties share a position, the next one skips) — copy
  `SkinsScoringEngine.BuildStandings`.
- If a result can be closed out, freeze it at the deciding hole and keep scoring the remaining holes
  into `Holes` (see match play's `DecidedOnHole`). Groups play out for other bets.
- Doc-comment the class with the rules in plain English, including tie handling.

Register it in `src/FairwayFinder.Features/ServiceRegistration.cs` next to the others:
`services.AddSingleton<IGameScoringEngine, {Name}ScoringEngine>();`

## Step 4 — Field validation (`Services/GameService.cs` → `ValidateField`)

Add a `case GameType.{Name}:` that refuses a field the engine can't score, returning
`GameResultStatus.ParticipantCountInvalid` (409 from the API) with a message that names the fix:
"High-Low needs two teams of the same size, at least two each." It runs at **start**, not create, so
hosts can build the field in any order.

## Step 5 — Rule options (only if the game has its own settings)

Settings are deliberately **typed columns on `game`, not jsonb**. For each option:

1. Property on `Game` + column mapping in `ApplicationDbContext` (snake_case, sensible default).
2. Migration — `MigrationTests` fails without one.
3. `CreateGameRequest` (`Features/Data/GameDtos.cs`) and the copy in `GameService.CreateGameAsync`.
4. Validation in `src/FairwayFinder.Api/Validators/GameValidators.cs`.
5. `GameRules` record (`Games/GameScoringContext.cs`) and its construction in `GameScoreReader`.
6. Update every `GameRules` builder: `tests/FairwayFinder.Features.Tests/Helpers/GameScoringTestData.Rules`
   and `GameCardModelTests.State` (grep `new GameRules(`).

## Step 6 — Labels

The display name lives in three switches (grep `GameType.HighLow =>`):

- `Admin/Components/Pages/Games/Pages/Games.razor` and `GameDetail.razor` → `FormatGameType`
- `Features/Helpers/GameNotifications.cs` → the push-notification noun

## Step 7 — Admin scorecard (`Admin/Components/Pages/Games/Components/GameCardModel.cs`)

The grid never inspects scoreboard types; add one case to `GameCardModel.From` and a private builder
that maps each settled hole to a `GameCardHole`:

- `Awards`: participant id → tag for the cells that won something. `null` = plain win; a short tag
  says what (`"L"`, `"H"`, `"×3"`). Light the golfer whose ball counted, not the whole side.
- `Result`: one chip per hole — the winning side's `Label` with its `SideIndex`, `"½"` for halved,
  or a neutral chip (`"Carry"`, `"1–1"`). Set `IsWin` for a sideless winner (individual games).
- `Standing`: where the game stands after the hole (`"2 UP"`, `"+3"`, `"AS"`), or `null` with a null
  `StandingLabel` to hide the row.
- `AfterDecided`: true for holes played after a frozen result.
- Individual games return no sides from `SidesFor` (like skins) so rows aren't colour-striped.

Show scores with `Shared/Scoring/ScoreMark`; never hand-roll birdie/bogey styling. If the game needs a
new kind of cell decoration, extend `GameCardHole` generically rather than special-casing the grid.

## Step 8 — Tests

| Where | What |
|---|---|
| `tests/FairwayFinder.Features.Tests/Games/{Name}ScoringEngineTests.cs` | The rules, one fact per behaviour, built with `GameScoringTestData` (`Context`, `Line`, `Rules`). Cover: a clean win, every tie rule, net strokes changing a result, an unsettled gap stopping the walk, early decision/freeze, standings ranking, empty/invalid field. |
| `tests/FairwayFinder.Features.Tests/GameServiceTests.cs` | `StartGameAsync` accepts the right field and refuses each wrong one, asserting the message. |
| Every test harness that builds a resolver | Add `new {Name}ScoringEngine()` — grep `GameScoringEngineResolver([` (currently 5 files). |
| `tests/FairwayFinder.Api.IntegrationTests/Endpoints/GameEndpointsTests.cs` | Create → add field → start (and one refused start) → score every hole → assert the raw JSON `scoreboard.gameType` and key fields → complete → the posted snapshot reads back as the same type. Copy `High_low_two_on_two_from_create_to_posted_result`. |
| `tests/FairwayFinder.Admin.IntegrationTests/Pages/GameCardModelTests.cs` | Awards land on the right golfer with the right tag, result/standing chips, after-decided fading if applicable. |

Then run `dotnet build FairwayFinder.sln` and `dotnet test` (integration tests need Docker).

## Step 9 — iOS (`../FairwayFinder-iOS`)

The server change alone isn't user-visible. In the iOS repo, `GameType` in `Models/GameModels.swift`
decodes an **unknown type as `.matchPlay`**, and `GameScoreboard` degrades an unknown tag to
`.unsupported` (standings only). So until the app ships support, older builds mislabel the game.
That repo's `game-hole-grid` skill covers the client side (model, decoding, hole grid). Tell the user
it's needed rather than doing it unasked.

## Done checklist

- [ ] Rules agreed and written into the engine's doc comment
- [ ] Enum value (reserved number respected), scoreboard + `JsonDerivedType`, engine, DI registration
- [ ] `ValidateField` case with actionable messages
- [ ] Rule options plumbed end to end + migration (if any)
- [ ] Three label switches
- [ ] `GameCardModel.From` case
- [ ] Engine, service, API round-trip, and card-model tests; all resolver harnesses updated
- [ ] Build and full test run green
- [ ] User told about the iOS follow-up
