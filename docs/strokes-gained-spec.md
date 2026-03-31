# Strokes Gained — Feature Specification

## 1. Overview

### What is Strokes Gained?

Strokes Gained (SG) is a statistical framework developed by Mark Broadie (Columbia Business School) that measures how many strokes a golfer gains or loses on **every shot** relative to a baseline. Unlike traditional stats (FIR%, GIR%, putts per round), strokes gained isolates the value of each part of the game by comparing actual performance against expected performance from a given distance and lie.

**The core formula (per shot):**

```
SG = Expected Strokes (start position) − Expected Strokes (end position) − (1 + penalty strokes)
```

For example, a golfer 150 yards out in the fairway (expected: 2.99 strokes to hole out) hits it to 10 feet on the green (expected: 1.61 strokes):

```
SG = 2.99 − 1.61 − 1 = +0.38 strokes gained
```

The sum of all per-shot SG values for a hole equals the total SG for that hole. The sum across all holes equals the round's total SG.

### Strokes Gained Categories

Each shot is classified into a category based on its starting lie and distance:

| Category | Abbreviation | What It Measures |
|---|---|---|
| SG: Off the Tee | SG:OTT | Tee shots on par 4s and par 5s |
| SG: Approach | SG:APP | Approach shots into the green (includes par 3 tee shots) |
| SG: Around the Green | SG:ARG | Shots within ~50 yards of the green, not on the green |
| SG: Putting | SG:P | All shots from the green |
| SG: Tee to Green | SG:T2G | SG:OTT + SG:APP + SG:ARG |
| SG: Total | SG:TOT | SG:T2G + SG:P |

### Shot Category Classification Rules

```
If StartLie == Green:
    → SG: Putting

If StartLie == Tee AND hole par >= 4:
    → SG: Off the Tee

If StartLie == Tee AND hole par == 3:
    → SG: Approach

If StartLie != Tee AND StartLie != Green AND StartDistance <= 50 yards:
    → SG: Around the Green

If StartLie != Tee AND StartLie != Green AND StartDistance > 50 yards:
    → SG: Approach
```

---

## 2. Data Model — Shot Entity

### The Shot Table

Every physical swing is recorded as a row in the `shot` table, linked to a `Score` (which represents a single hole in a round). The total number of shots + penalty strokes on a hole must equal the hole score.

### Entity: `Shot`

```csharp
namespace FairwayFinder.Data.Entities;

public partial class Shot
{
    public long ShotId { get; set; }
    public long ScoreId { get; set; }           // FK to Score — which hole this shot belongs to

    public int ShotNumber { get; set; }          // 1-based sequence of physical swings on this hole

    // Starting position (before the swing)
    public int StartDistance { get; set; }            // distance value
    public string StartDistanceUnit { get; set; } = null!;  // "yards" or "feet"
    public int StartLie { get; set; }                 // LieType enum stored as int

    // Ending position (after the swing) — null if holed out
    public int? EndDistance { get; set; }             // null = ball is in the hole
    public string? EndDistanceUnit { get; set; }     // null = ball is in the hole; "yards" or "feet"
    public int? EndLie { get; set; }                  // null = ball is in the hole

    public int PenaltyStrokes { get; set; }      // 0 = clean shot, 1 = OB/hazard penalty, 2 = rare

    // Audit fields (matching existing entity pattern)
    public string CreatedBy { get; set; } = null!;
    public DateOnly CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = null!;
    public DateOnly UpdatedOn { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Score Score { get; set; } = null!;
}
```

### Key Design Decisions

**Each row = 1 physical swing**, not 1 scorecard stroke. Penalty strokes are tracked per-shot via `PenaltyStrokes`. The hole score is validated as:

```
HoleScore = count(shots) + sum(penalty_strokes)
```

**Distance units are explicit:** Each distance field has a corresponding `DistanceUnit` column (`Yards` or `Feet`) stored on the row so the database is unambiguous. Convention: when the lie is `Green`, the unit is `Feet`; for all other lies, the unit is `Yards`. The UI auto-sets this based on lie selection, but the stored unit is the source of truth.

**End position of shot N = Start position of shot N+1.** This is enforced by the UI but stored redundantly per shot so each row is self-contained for SG calculation without joining to adjacent shots.

**Holed out:** The final shot on every hole has `EndDistance = null` and `EndLie = null`, meaning the ball is in the cup. Expected strokes for "holed" = 0.

### Penalty Shot Examples

**OB off the tee (stroke and distance):**

| Shot# | Start | StartLie | End | EndLie | Penalty | Card Stroke |
|---|---|---|---|---|---|---|
| 1 | 400yd | Tee | 400yd | Tee | 1 | 1+2 (re-teeing) |
| 2 | 400yd | Tee | 150yd | Fairway | 0 | 3 |
| 3 | 150yd | Fairway | 20ft | Green | 0 | 4 |
| 4 | 20ft | Green | null | null | 0 | 5 (holed) |

Physical shots: 4. Penalty strokes: 1. Hole score: 5 (bogey on par 4). ✓

SG for shot 1: `expected(400, Tee) − expected(400, Tee) − (1+1) = 0 − 2 = −2.0`
This correctly captures that hitting OB costs ~2 strokes vs baseline.

**Water hazard on approach (1-stroke penalty, drop near hazard):**

| Shot# | Start | StartLie | End | EndLie | Penalty | Card Stroke |
|---|---|---|---|---|---|---|
| 1 | 380yd | Tee | 160yd | Fairway | 0 | 1 |
| 2 | 160yd | Fairway | 100yd | Rough | 1 | 2+3 (penalty drop) |
| 3 | 100yd | Rough | 15ft | Green | 0 | 4 |
| 4 | 15ft | Green | null | null | 0 | 5 (holed) |

Physical shots: 4. Penalty strokes: 1. Hole score: 5 (bogey on par 4). ✓

---

## 3. Enums

### LieType

```csharp
namespace FairwayFinder.Features.Enums;

public enum LieType
{
    Tee = 0,
    Fairway = 1,
    Rough = 2,
    Sand = 3,        // waste area, general sand (not a defined bunker)
    Bunker = 4,      // greenside or fairway bunker
    Recovery = 5,    // trees, behind obstruction, severely plugged
    Green = 6
}
```

> **Sand vs Bunker:** `Sand` is for waste areas or general sandy lies that are not a formal bunker. `Bunker` is for greenside and fairway bunkers. Both use the same baseline expected strokes data (Broadie's "Sand" column), but tracking them separately provides more granular analysis.

### DistanceUnit

Stored as a plain string on every distance field so the database is human-readable and unambiguous. Not an int-backed enum — the column contains the literal text `"yards"` or `"feet"`.

```csharp
namespace FairwayFinder.Features.Enums;

public static class DistanceUnit
{
    public const string Yards = "yards";
    public const string Feet = "feet";
}
```

### BaselineLevel

The baseline level determines which benchmark data is used for comparison. Scratch is the industry standard.

```csharp
namespace FairwayFinder.Features.Enums;

public enum BaselineLevel
{
    Scratch = 0,       // 0 handicap
    Bogey = 1,         // ~18 handicap
    HighHandicap = 2   // ~30 handicap
}
```

### ShotCategory (derived, not stored)

```csharp
namespace FairwayFinder.Features.Enums;

public enum ShotCategory
{
    OffTheTee,
    Approach,
    AroundTheGreen,
    Putting
}
```

---

## 4. Baseline Data — Expected Strokes Reference

### What It Is

A static lookup table that answers: **"From X distance, in lie Y, how many strokes does the baseline golfer need to hole out?"**

This is the foundation of all SG calculations. The data comes from Mark Broadie's research (*Every Shot Counts*, 2014), which is also the basis for the PGA Tour's strokes gained implementation.

### Storage

Static in-code lookup (not database). Reasons:
- Fixed reference data, never user-modified
- Must be fast (called per-shot for every SG calculation)
- Easy to version, test, and swap between baseline levels
- No need for SQL queries

### Expected Strokes by Distance and Lie (Scratch Baseline, Selected Data Points)

| Distance | Tee | Fairway | Rough | Sand/Bunker | Recovery | Green (feet) | Green Exp. Putts |
|---|---|---|---|---|---|---|---|
| 600yd | 4.73 | — | — | — | — | — | — |
| 500yd | 4.40 | 4.57 | 4.73 | — | — | — | — |
| 450yd | 4.18 | 4.35 | 4.61 | — | — | — | — |
| 400yd | 3.99 | 4.08 | 4.34 | — | — | — | — |
| 350yd | 3.86 | 3.84 | 4.08 | — | — | — | — |
| 300yd | 3.71 | 3.64 | 3.84 | 3.80 | 4.20 | — | — |
| 250yd | 3.54 | 3.45 | 3.62 | 3.57 | 3.95 | — | — |
| 200yd | 3.35 | 3.18 | 3.35 | 3.36 | 3.70 | — | — |
| 150yd | 3.09 | 2.99 | 3.12 | 3.15 | 3.45 | — | — |
| 125yd | 2.97 | 2.87 | 2.99 | 3.06 | 3.30 | — | — |
| 100yd | 2.82 | 2.75 | 2.86 | 2.94 | 3.15 | — | — |
| 75yd | 2.63 | 2.63 | 2.74 | 2.82 | 3.00 | — | — |
| 50yd | — | 2.54 | 2.63 | 2.68 | 2.85 | — | — |
| 40yd | — | 2.47 | 2.56 | 2.61 | 2.78 | — | — |
| 30yd | — | 2.40 | 2.49 | 2.53 | 2.70 | — | — |
| 20yd | — | 2.33 | 2.42 | 2.47 | 2.63 | 60ft | 2.11 |
| 10yd | — | 2.18 | 2.28 | 2.38 | 2.50 | 30ft | 1.96 |
| — | — | — | — | — | — | 20ft | 1.87 |
| — | — | — | — | — | — | 15ft | 1.78 |
| — | — | — | — | — | — | 10ft | 1.61 |
| — | — | — | — | — | — | 8ft | 1.50 |
| — | — | — | — | — | — | 5ft | 1.15 |
| — | — | — | — | — | — | 3ft | 1.04 |
| — | — | — | — | — | — | 2ft | 1.01 |

> `Sand` and `Bunker` lie types share the same baseline column. They are tracked separately for analysis granularity but use identical expected strokes values.

> Full lookup should have entries at every 5–10 yard interval (non-green) and every 1–5 foot interval (green) with linear interpolation between points.

### Implementation

```csharp
namespace FairwayFinder.Features.Helpers;

public static class StrokesGainedBaseline
{
    /// <summary>
    /// Returns expected strokes to hole out from a given distance and lie.
    /// The DistanceUnit specifies whether the distance is in yards or feet.
    /// Sand and Bunker lie types use the same baseline column.
    /// </summary>
    public static double GetExpectedStrokes(int distance, string unit, LieType lie, BaselineLevel level = BaselineLevel.Scratch)

    /// <summary>
    /// Returns 0.0 — used when the ball is holed (EndDistance is null).
    /// </summary>
    public static double GetExpectedStrokesHoled() => 0.0;

    /// <summary>
    /// Linear interpolation between two data points for distances not in the table.
    /// </summary>
    private static double Interpolate(double x, double x0, double y0, double x1, double y1)
}
```

---

## 5. Calculation Engine

### Per-Shot SG

```csharp
namespace FairwayFinder.Features.Helpers;

public static class StrokesGainedCalculator
{
    /// <summary>
    /// Calculates strokes gained for a single shot.
    /// Distance values are interpreted using their DistanceUnit (Yards or Feet).
    /// Sand and Bunker lie types use the same baseline data.
    /// </summary>
    public static double CalculateShotSg(
        int startDistance, string startUnit, LieType startLie,
        int? endDistance, string? endUnit, LieType? endLie,
        int penaltyStrokes,
        BaselineLevel level = BaselineLevel.Scratch)
    {
        var expectedStart = StrokesGainedBaseline.GetExpectedStrokes(startDistance, startUnit, startLie, level);

        var expectedEnd = (endDistance == null || endLie == null)
            ? 0.0  // holed out
            : StrokesGainedBaseline.GetExpectedStrokes(endDistance.Value, endUnit!, endLie.Value, level);

        return expectedStart - expectedEnd - (1 + penaltyStrokes);
    }

    /// <summary>
    /// Classifies a shot into an SG category based on start position and hole par.
    /// </summary>
    public static ShotCategory ClassifyShot(LieType startLie, int startDistance, int holePar)
    {
        if (startLie == LieType.Green)
            return ShotCategory.Putting;
        if (startLie == LieType.Tee && holePar >= 4)
            return ShotCategory.OffTheTee;
        if (startLie == LieType.Tee && holePar == 3)
            return ShotCategory.Approach;
        if (startDistance <= 50)
            return ShotCategory.AroundTheGreen;
        return ShotCategory.Approach;
    }
}
```

### Per-Hole SG

Aggregates all shots for a single hole and breaks down by category.

```csharp
    /// <summary>
    /// Calculates the SG breakdown for a single hole from its shots.
    /// </summary>
    public static StrokesGainedHoleResult CalculateHoleSg(
        List<ShotData> shots, int holePar, BaselineLevel level = BaselineLevel.Scratch)
```

### Per-Round SG

Sums per-hole results across all 9 or 18 holes.

```csharp
    /// <summary>
    /// Calculates the SG summary for an entire round.
    /// </summary>
    public static StrokesGainedSummary CalculateRoundSg(
        RoundResponse round, BaselineLevel level = BaselineLevel.Scratch)
```

### Multi-Round Aggregation

Averages SG across multiple rounds and computes trends.

```csharp
    /// <summary>
    /// Average SG stats across multiple rounds.
    /// </summary>
    public static StrokesGainedSummary CalculateAverageSg(
        List<RoundResponse> rounds, BaselineLevel level = BaselineLevel.Scratch)

    /// <summary>
    /// SG trend over time for a specific category (for charting).
    /// Uses the same linear regression approach as existing trend calculations.
    /// </summary>
    public static (List<StrokesGainedTrendPoint> Points, double? Slope) CalculateSgTrend(
        List<RoundResponse> rounds, ShotCategory? category, BaselineLevel level = BaselineLevel.Scratch)
```

---

## 6. DTOs

### Shot Data (Input/Display)

```csharp
namespace FairwayFinder.Features.Data;

/// <summary>
/// A single shot's data — used for both input (round entry) and display (round view).
/// </summary>
public class ShotData
{
    public long ShotId { get; set; }
    public int ShotNumber { get; set; }

    public int StartDistance { get; set; }
    public string StartDistanceUnit { get; set; } = DistanceUnit.Yards;
    public LieType StartLie { get; set; }

    public int? EndDistance { get; set; }
    public string? EndDistanceUnit { get; set; }
    public LieType? EndLie { get; set; }

    public int PenaltyStrokes { get; set; }

    // Computed (set by calculator, not by user)
    public double? StrokesGained { get; set; }
    public ShotCategory? Category { get; set; }
}
```

### Per-Hole SG Result

```csharp
/// <summary>
/// Strokes gained breakdown for a single hole.
/// </summary>
public class StrokesGainedHoleResult
{
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int Score { get; set; }

    public double SgTotal { get; set; }
    public double SgPutting { get; set; }
    public double SgOffTheTee { get; set; }
    public double SgApproach { get; set; }
    public double SgAroundTheGreen { get; set; }

    public List<ShotData> Shots { get; set; } = new();
}
```

### Round/Aggregate SG Summary

```csharp
/// <summary>
/// Strokes gained summary for a round or aggregated across multiple rounds.
/// All values are totals (for a single round) or per-round averages (for aggregates).
/// </summary>
public class StrokesGainedSummary
{
    public double SgTotal { get; set; }
    public double SgPutting { get; set; }
    public double SgTeeToGreen { get; set; }
    public double SgOffTheTee { get; set; }
    public double SgApproach { get; set; }
    public double SgAroundTheGreen { get; set; }

    public int RoundsIncluded { get; set; }
    public int HolesWithShots { get; set; }     // holes that have shot data

    // Trends (set on aggregate summaries, null on single-round)
    public double? SgTotalTrend { get; set; }
    public double? SgPuttingTrend { get; set; }
    public double? SgTeeToGreenTrend { get; set; }
    public double? SgOffTheTeeTrend { get; set; }
    public double? SgApproachTrend { get; set; }
    public double? SgAroundTheGreenTrend { get; set; }
}
```

### SG Trend Point (for charts)

```csharp
/// <summary>
/// A single data point for SG trend charting.
/// </summary>
public class StrokesGainedTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public double Value { get; set; }
    public double? MovingAverage { get; set; }
}
```

### Updates to Existing DTOs

**`RoundResponse`** — add:
```csharp
    // Shot-by-shot data and SG
    public bool UsingShotTracking { get; set; }
    public StrokesGainedSummary? StrokesGained { get; set; }
    public List<StrokesGainedHoleResult>? HoleByHoleSg { get; set; }
```

**`RoundHole`** — add:
```csharp
    // Shot data (null if not tracking shots)
    public List<ShotData>? Shots { get; set; }
```

**`UserStatsResponse`** — add:
```csharp
    // Strokes Gained
    public StrokesGainedSummary? StrokesGained { get; set; }
    public List<StrokesGainedTrendPoint> SgTotalTrend { get; set; } = new();
    public List<StrokesGainedTrendPoint> SgPuttingTrend { get; set; } = new();
    public List<StrokesGainedTrendPoint> SgTeeToGreenTrend { get; set; } = new();
```

**`CourseStatsResponse`** — add:
```csharp
    public StrokesGainedSummary? StrokesGained { get; set; }
    public List<StrokesGainedHoleResult>? PerHoleSg { get; set; }  // average SG by hole number
```

---

## 7. Database Changes

### New Table: `shot`

```sql
CREATE TABLE shot (
    shot_id              BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    score_id             BIGINT NOT NULL REFERENCES score(score_id) ON DELETE RESTRICT,

    shot_number          INT NOT NULL,

    start_distance       INT NOT NULL,
    start_distance_unit  TEXT NOT NULL,     -- 'yards' or 'feet'
    start_lie            INT NOT NULL,

    end_distance         INT,
    end_distance_unit    TEXT,              -- 'yards' or 'feet'; null when holed
    end_lie              INT,

    penalty_strokes      INT NOT NULL DEFAULT 0,

    created_by           TEXT NOT NULL,
    created_on           DATE NOT NULL,
    updated_by           TEXT NOT NULL,
    updated_on           DATE NOT NULL,
    is_deleted           BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX ix_shot_score_id ON shot(score_id);
```

### DbContext Configuration

```csharp
// Shot
modelBuilder.Entity<Shot>(entity =>
{
    entity.HasKey(e => e.ShotId).HasName("shot_pkey");
    entity.ToTable("shot");
    entity.Property(e => e.ShotId).HasColumnName("shot_id");
    entity.Property(e => e.ScoreId).HasColumnName("score_id");
    entity.Property(e => e.ShotNumber).HasColumnName("shot_number");
    entity.Property(e => e.StartDistance).HasColumnName("start_distance");
    entity.Property(e => e.StartDistanceUnit).HasColumnName("start_distance_unit");
    entity.Property(e => e.StartLie).HasColumnName("start_lie");
    entity.Property(e => e.EndDistance).HasColumnName("end_distance");
    entity.Property(e => e.EndDistanceUnit).HasColumnName("end_distance_unit");
    entity.Property(e => e.EndLie).HasColumnName("end_lie");
    entity.Property(e => e.PenaltyStrokes).HasColumnName("penalty_strokes").HasDefaultValue(0);
    entity.Property(e => e.CreatedBy).HasColumnName("created_by");
    entity.Property(e => e.CreatedOn).HasColumnName("created_on");
    entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
    entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
    entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");

    entity.HasOne(e => e.Score).WithMany().HasForeignKey(e => e.ScoreId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

### DbSet

```csharp
public virtual DbSet<Shot> Shots { get; set; }
```

### Round Entity Change

Add a flag to `Round` to indicate shot-by-shot tracking is enabled:

```csharp
public bool UsingShotTracking { get; set; }
```

Column: `using_shot_tracking BOOLEAN NOT NULL DEFAULT FALSE`

### Relationship to HoleStat

`HoleStat` and `Shot` **coexist**. They serve different purposes:

| | HoleStat | Shot |
|---|---|---|
| Granularity | 1 per hole | N per hole (1 per swing) |
| Purpose | Quick aggregate stats (FIR, GIR, putts) | Full shot data for SG |
| Required for SG? | No | Yes |
| Can derive the other? | HoleStat fields derivable from shots | No |

When `UsingShotTracking = true`, the `HoleStat` record can be **auto-populated from shot data** during round creation:
- `NumberOfPutts` = count of shots where `StartLie == Green`
- `HitFairway` = shot 1 EndLie == Fairway (par 4/5 only)
- `HitGreen` = the shot before the first putt ended on the green in regulation
- `ApproachYardage` = StartDistance of the shot classified as SG:Approach
- `TeeShotOb` = shot 1 has PenaltyStrokes > 0
- `ApproachShotOb` = approach shot has PenaltyStrokes > 0

This means **all existing stats (FIR%, GIR%, putts, trends) continue to work** even for shot-tracked rounds, because HoleStat is still populated.

---

## 8. Service Layer Changes

### RoundService

**New methods:**

```csharp
/// <summary>
/// Gets all shots for a round, grouped by ScoreId (hole).
/// </summary>
Task<Dictionary<long, List<ShotData>>> GetShotsByRoundIdAsync(long roundId)
```

**Modified methods:**

`CreateRoundAsync` — when `UsingShotTracking = true`:
1. Create Round, Score entities (same as today)
2. Create Shot entities from the shot data in the request
3. **Auto-derive** HoleStat from shots (FIR, GIR, putts, approach yardage, OB)
4. Create RoundStat (scoring distribution — same as today)
5. Validate: for each hole, `count(shots) + sum(penalty_strokes) == hole_score`

`UpdateRoundAsync` — same flow, with shot upsert (delete old shots for a hole, insert new ones).

`GetRoundsWithDetailsAsync` — add optional include for shots when needed for SG calculations.

**Request DTO update:**

```csharp
public class HoleScoreEntry
{
    // ... existing fields ...

    // Shot-by-shot data (populated when UsingShotTracking = true)
    public List<ShotData>? Shots { get; set; }
}
```

### StatsService

**No new service methods.** SG is computed within existing `GetUserStatsAsync` and `GetCourseStatsAsync` flows:

```csharp
// In GetUserStatsAsync, after existing stats calculation:
var roundsWithShots = filteredRounds.Where(r => r.UsingShotTracking).ToList();
if (roundsWithShots.Any())
{
    response.StrokesGained = StrokesGainedCalculator.CalculateAverageSg(roundsWithShots, baselineLevel);
    // ... trend calculations ...
}
```

### Data Loading

When SG stats are needed, shots must be loaded. Two options:

**Option A: Always load shots with rounds** — Simple but potentially heavy if a user has many rounds with 70+ shots each.

**Option B: Lazy load shots only when computing SG** — Keeps the existing query fast and loads shots in a separate query only when needed.

**Recommendation: Option B.** The existing `GetRoundsWithDetailsAsync` stays unchanged. A new `LoadShotsForRoundsAsync` method fetches shots for rounds that have `UsingShotTracking = true`, and attaches them to the RoundResponse objects before SG calculation.

---

## 9. UI Changes

### Round Entry — Shot Tracking Mode

The existing 3-step round creation wizard gets a new tracking mode. In **Step 1 (Setup)**, alongside the existing "Track Advanced Stats" toggle, add:

```
Tracking Mode:
  ○ Scorecard Only       — just scores
  ○ Advanced Stats       — scores + FIR, GIR, putts, approach yardage (existing)
  ○ Shot-by-Shot         — full shot tracking with strokes gained
```

When "Shot-by-Shot" is selected, the **Step 2 (Scorecard)** view changes significantly.

#### Shot Entry Per Hole

Instead of (or in addition to) the single-row scorecard, each hole has an expandable shot list. For each hole:

1. **Shot 1 is pre-populated:** StartDistance = hole yardage, StartLie = Tee
2. User enters: **End Distance**, **End Lie**, and optionally **Penalty Strokes**
3. If not holed, Shot 2 auto-populates: StartDistance = Shot 1's EndDistance, StartLie = Shot 1's EndLie
4. Repeat until the user marks the final shot as "Holed" (EndDistance/EndLie = null)
5. Score is **auto-calculated** from shots: `count(shots) + sum(penalties)`

#### Shot Entry UI Pattern

Per hole, a compact shot list:

```
Hole 7 — Par 4, 385 yards                    Score: 5 (+1)
┌────────────────────────────────────────────────────────────┐
│ #  │ From           │ To             │ Penalty │ SG        │
│ 1  │ 385yd Tee      │ 145yd Fairway  │ —       │ +0.12     │
│ 2  │ 145yd Fairway  │ 25ft Green     │ —       │ +0.16     │
│ 3  │ 25ft Green     │ 4ft Green      │ —       │ -0.11     │
│ 4  │ 4ft Green      │ Holed          │ —       │ +0.01     │
│                                                  SG: +0.18 │
│ [+ Add Shot]  [Undo Last]                                  │
└────────────────────────────────────────────────────────────┘
```

- **From** is auto-filled from the previous shot's result (or tee for shot 1)
- **To** is entered by the user: distance input + lie dropdown
- **"Holed" button** marks the final shot
- **Penalty** is a toggle/counter that adds to the stroke count
- **SG** is computed in real-time as shots are entered (gives immediate feedback)
- **Score** is auto-derived, not manually entered

#### Lie Type Input

A simple button group or segmented control for lie selection:

```
[ Tee ] [ Fairway ] [ Rough ] [ Bunker ] [ Sand ] [ Recovery ] [ Green ]
```

When `Green` is selected, the distance unit auto-switches to "feet". For all other lies, defaults to "yards". The unit is stored explicitly on the row via `DistanceUnit`.

#### Compact Scorecard Summary

Above or alongside the shot entry, maintain the existing scorecard row view showing:
- Hole numbers, par, running score total
- Per-hole SG value (color-coded: green = positive, red = negative)
- This gives a familiar scorecard feel while shot entry happens per-hole below

### Dashboard — Strokes Gained Section

Add a new section to `FairwayFinderDashboard.razor`, below the existing Advanced Stats cards.

**Only shown when the user has rounds with shot tracking.**

#### SG Stats Cards

Row of `FairwayFinderStatsCard` components:

```
┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│  SG: Total  │ │ SG: Putting │ │   SG: OTT   │ │ SG: Approach│ │   SG: ARG   │
│   -0.42     │ │   +0.31     │ │   -0.15     │ │   -0.38     │ │   -0.20     │
│  ↗ Trend    │ │  ↘ Trend    │ │  → Trend    │ │  ↗ Trend    │ │  → Trend    │
└─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘
```

- Values are **per-round averages**
- Positive = green (gaining on baseline), negative = red (losing)
- Trend arrow from linear regression (same pattern as existing FIR/GIR trends)
- Click → opens trend dialog

#### SG Trend Chart

Line chart (same style as `FairwayFinderScoreTrendChart`):
- X-axis: date/round sequence
- Y-axis: SG value per round
- Toggle between: SG:Total, SG:Putting, SG:T2G (via `RadzenSelectBar`)
- Zero line highlighted (above = gaining, below = losing)
- Moving average overlay (dashed line)

#### SG Category Breakdown Chart

Horizontal bar chart showing contribution of each category:
- Bars extend left (losing strokes) or right (gaining)
- Categories: Putting, Off the Tee, Approach, Around the Green
- Helps answer "where do I lose the most strokes?"

### View Round — Per-Shot SG Display

On `ViewRound.razor`, when the round has shot tracking:

#### SG Summary Bar

Compact row at the top of the stats panel:

```
SG: Total -1.2 | Putting +0.8 | OTT -0.5 | Approach -1.2 | ARG -0.3
```

#### Per-Hole SG Row in Scorecard

New row in `FairwayFinderScorecard`:

```
Hole    | 1     | 2     | 3     | ...  | Out   | 10    | ...  | Total
Par     | 4     | 3     | 5     |      |       | 4     |      |
Score   | 5     | 3     | 4     |      |       | 6     |      |
SG      | -0.82 | +0.18 | +1.05 |      | +0.41 | -1.50 |      | -1.20
```

Color-coded cells (green positive, red negative).

#### Expandable Shot Detail Per Hole

Click a hole to expand and see the shot-by-shot breakdown:

```
▼ Hole 7 — Par 4, 385 yards — Score: 5 — SG: +0.18
  Shot 1: 385yd Tee → 145yd Fairway         SG:OTT  +0.12
  Shot 2: 145yd Fairway → 25ft Green         SG:APP  +0.16
  Shot 3: 25ft Green → 4ft Green             SG:P    -0.11
  Shot 4: 4ft Green → Holed                  SG:P    +0.01
```

#### SG Breakdown Table

In `RoundStatsPanel`, a new section:

| Category | Total SG | Per Hole Avg | Assessment |
|---|---|---|---|
| SG: Total | -1.20 | -0.07 | Below baseline |
| SG: Putting | +0.80 | +0.04 | Above baseline |
| SG: Off the Tee | -0.50 | -0.04 | Slightly below |
| SG: Approach | -1.20 | -0.08 | Needs work |
| SG: Around the Green | -0.30 | -0.04 | Slightly below |

### Course Stats — SG by Hole

Add SG columns to `CourseHoleStatsGrid`:

| Hole | Par | Avg Score | SG: Total | SG: Putting | SG: T2G |
|---|---|---|---|---|---|
| 1 | 4 | 4.8 | -0.62 | +0.08 | -0.70 |
| 2 | 3 | 3.2 | -0.14 | -0.02 | -0.12 |
| ... | | | | | |

### Baseline Selector

Add a dropdown to dashboard/stats filters:

```
Baseline: [ Scratch (0 HCP) ▼ ]
Options: Scratch (0 HCP), Bogey (~18 HCP), High Handicap (~30 HCP)
```

### SG Trend Dialogs

New dialog components (matching existing pattern of `FirTrendDialog`, `GirTrendDialog`, `PuttsTrendDialog`):

- `SgTotalTrendDialog.razor`
- `SgPuttingTrendDialog.razor`
- `SgTeeToGreenTrendDialog.razor`

Each shows a line chart with historical per-round SG values and a trend line.

---

## 10. File Changes Summary

### New Files

| File | Project | Purpose |
|---|---|---|
| `Entities/Shot.cs` | Data | Shot entity |
| `Migrations/AddShotTable.cs` | Data | EF migration for shot table + round flag |
| `Enums/LieType.cs` | Features | Tee/Fairway/Rough/Sand/Bunker/Recovery/Green |
| `Enums/DistanceUnit.cs` | Features | String constants: `"yards"`, `"feet"` |
| `Enums/BaselineLevel.cs` | Features | Scratch/Bogey/HighHandicap |
| `Enums/ShotCategory.cs` | Features | OTT/Approach/ARG/Putting |
| `Helpers/StrokesGainedBaseline.cs` | Features | Static baseline expected strokes lookup |
| `Helpers/StrokesGainedCalculator.cs` | Features | All SG calculation methods |
| `Data/ShotData.cs` | Features | Shot input/display DTO |
| `Data/StrokesGainedHoleResult.cs` | Features | Per-hole SG result DTO |
| `Data/StrokesGainedSummary.cs` | Features | Round/aggregate SG summary DTO |
| `Data/StrokesGainedTrendPoint.cs` | Features | SG trend data for charts |
| `Pages/Home/Components/StrokesGainedCards.razor` | Web | SG stats card row |
| `Pages/Home/Components/StrokesGainedTrendChart.razor` | Web | SG trend line chart |
| `Pages/Home/Components/StrokesGainedBreakdownChart.razor` | Web | SG category bar chart |
| `Pages/Home/Dialogs/SgTotalTrendDialog.razor` | Web | SG:Total trend dialog |
| `Pages/Home/Dialogs/SgPuttingTrendDialog.razor` | Web | SG:Putting trend dialog |
| `Pages/Home/Dialogs/SgTeeToGreenTrendDialog.razor` | Web | SG:T2G trend dialog |
| `Pages/Rounds/Components/ShotEntryHole.razor` | Web | Per-hole shot entry component |
| `Pages/Rounds/Components/ShotList.razor` | Web | Shot list display for round view |
| `Helpers/StrokesGainedCalculatorTests.cs` | Tests | SG calculation unit tests |
| `Helpers/StrokesGainedBaselineTests.cs` | Tests | Baseline lookup unit tests |

### Modified Files

| File | Project | Change |
|---|---|---|
| `Entities/Round.cs` | Data | Add `UsingShotTracking` bool |
| `ApplicationDbContext.cs` | Data | Add `DbSet<Shot>`, Shot config, Round column |
| `Data/RoundResponse.cs` | Features | Add shot/SG fields to RoundResponse and RoundHole |
| `Data/UserStatsResponse.cs` | Features | Add SG summary + trend fields |
| `Data/CourseStatsResponse.cs` | Features | Add SG summary + per-hole SG |
| `Data/CreateRoundRequest.cs` | Features | Add shot data to HoleScoreEntry |
| `Services/RoundService.cs` | Features | Persist shots, derive HoleStat from shots, load shots |
| `Services/StatsService.cs` | Features | Compute SG via StrokesGainedCalculator |
| `ServiceRegistration.cs` | Features | No new services (SG is static calculations) |
| `Pages/Home/Components/FairwayFinderDashboard.razor` | Web | Add SG section |
| `Pages/Rounds/Pages/ViewRound.razor` | Web | Show SG data |
| `Pages/Rounds/Components/RoundStatsPanel.razor` | Web | Add SG breakdown |
| `Pages/Rounds/Components/FairwayFinderScorecard.razor` | Web | Add SG row |
| `Pages/Rounds/Components/CreateRoundSetup.razor` | Web | Add shot tracking mode |
| `Pages/Rounds/Components/CreateRoundScorecard.razor` | Web | Shot entry UI |
| `Pages/Rounds/Components/CreateRoundReview.razor` | Web | Show SG in review |
| `Pages/Course/Pages/CourseStats.razor` | Web | Add SG display |
| `Pages/Course/Components/CourseHoleStatsGrid.razor` | Web | Add SG columns |

---

## 11. Validation Rules

### Shot Data Integrity

For each hole when `UsingShotTracking = true`:

1. **At least 1 shot** per hole
2. **Shot 1 StartDistance** must equal the hole yardage and StartLie must be Tee
3. **Shot N EndDistance/EndLie** must equal Shot N+1 StartDistance/StartLie (chain continuity)
4. **Last shot** must have EndDistance = null and EndLie = null (holed out)
5. **Score validation:** `count(shots) + sum(penalty_strokes) == hole_score`
6. **DistanceUnit consistency:** When `StartLie == Green`, `StartDistanceUnit` must be `Feet`; for all other lies, `Yards`. Same for end. UI auto-sets this.
7. **PenaltyStrokes** must be 0, 1, or 2
8. **Distance** must be > 0 for start, and > 0 or null for end

### Auto-Derive HoleStat from Shots

When shots are present, HoleStat is derived (not manually entered):

```csharp
HoleStat.NumberOfPutts = shots.Count(s => s.StartLie == LieType.Green);
HoleStat.HitFairway = shots[0].EndLie == LieType.Fairway;  // par 4/5 only
HoleStat.HitGreen = /* shot before first putt ended on green, in regulation */;
HoleStat.ApproachYardage = /* StartDistance of the approach shot */;
HoleStat.TeeShotOb = shots[0].PenaltyStrokes > 0;
HoleStat.ApproachShotOb = /* approach shot has penalty */;
```

"In regulation" for GIR: the shot that reaches the green must be shot number ≤ (par - 2), accounting for penalties.

---

## 12. Testing Strategy

### StrokesGainedBaseline Tests

```csharp
[Fact] Expected_strokes_from_tee_400y_returns_correct_value()
[Fact] Expected_strokes_from_fairway_150y_returns_correct_value()
[Fact] Expected_strokes_interpolates_between_data_points()
[Fact] Expected_strokes_green_20ft_returns_correct_value()
[Fact] Expected_strokes_holed_returns_zero()
[Fact] Different_baseline_levels_return_different_values()
[Fact] Sand_and_bunker_return_same_expected_strokes()
[Fact] Edge_case_very_short_distance()
[Fact] Edge_case_very_long_distance()
```

### StrokesGainedCalculator Tests

```csharp
// Per-shot
[Fact] Shot_sg_approach_to_green_positive_when_good()
[Fact] Shot_sg_penalty_shot_returns_approximately_negative_two()
[Fact] Shot_sg_holed_shot_uses_zero_expected()
[Fact] Shot_sg_three_putt_returns_negative()
[Fact] Shot_sg_one_putt_long_distance_returns_positive()

// Shot classification
[Fact] Classify_tee_shot_par4_returns_off_the_tee()
[Fact] Classify_tee_shot_par3_returns_approach()
[Fact] Classify_green_shot_returns_putting()
[Fact] Classify_30yd_rough_returns_around_the_green()
[Fact] Classify_100yd_fairway_returns_approach()
[Fact] Classify_40yd_bunker_returns_around_the_green()
[Fact] Classify_150yd_bunker_returns_approach()

// Per-hole
[Fact] Hole_sg_sums_all_shots_correctly()
[Fact] Hole_sg_categories_sum_to_total()
[Fact] Hole_sg_par_with_routine_shots_near_zero()
[Fact] Hole_sg_birdie_returns_positive_total()
[Fact] Hole_sg_with_penalty_shot_reflects_penalty()

// Per-round
[Fact] Round_sg_sums_all_holes()
[Fact] Round_sg_handles_9_hole_rounds()
[Fact] Round_sg_t2g_equals_ott_plus_app_plus_arg()

// Multi-round aggregation
[Fact] Average_sg_across_rounds()
[Fact] Sg_trend_returns_chronological_points()
[Fact] Sg_trend_calculates_regression_slope()

// HoleStat derivation
[Fact] Derive_putts_from_shots()
[Fact] Derive_fir_from_tee_shot_end_lie()
[Fact] Derive_gir_when_on_green_in_regulation()
[Fact] Derive_gir_false_when_not_in_regulation()
[Fact] Derive_approach_yardage_from_approach_shot()
[Fact] Derive_tee_shot_ob_from_penalty()

// Validation
[Fact] Validate_shots_score_matches_shot_count_plus_penalties()
[Fact] Validate_shot_chain_continuity()
[Fact] Validate_first_shot_starts_from_tee()
[Fact] Validate_last_shot_is_holed()
```

---

## 13. Resolved Design Decisions

| # | Decision | Resolution |
|---|---|---|
| 1 | **Default baseline** | Scratch (0 HCP) — industry standard, comparable across users |
| 2 | **SG on rounds without shot data** | Do not display any SG data. SG section only appears for rounds with shot tracking |
| 3 | **Mobile UX** | Not in scope for initial implementation. Desktop-first |
| 4 | **SG on public profiles** | Yes — show SG data on public profiles |
| 5 | **Computed SG storage** | Compute on-the-fly — baseline can change, matches existing stats pattern |
| 6 | **Mix tracking modes per round** | No — a round is either fully shot-tracked or not. Data must be consistent |
| 7 | **Auto-derive HoleStat from shots** | Yes, always — prevents data inconsistency between shots and HoleStat. No user override |
| 8 | **Around the green threshold** | 50 yards — matches PGA Tour definition |
| 9 | **Distance unit handling** | Single distance field + explicit `TEXT` column stored on every row (`"yards"` or `"feet"`). Human-readable in the database. UI auto-sets based on lie (Green → feet, all others → yards) |
| 10 | **Min rounds for SG trends** | 3 rounds minimum |

---

## 14. Glossary

| Term | Definition |
|---|---|
| **Strokes Gained** | Strokes better or worse than baseline from a given position on a single shot |
| **Baseline** | Reference performance level (scratch, bogey, etc.) for comparison |
| **Expected Strokes** | Average strokes to hole out from a given distance and lie per the baseline |
| **SG: OTT** | Strokes Gained: Off the Tee — tee shot quality on par 4s/5s |
| **SG: APP** | Strokes Gained: Approach — approach shots (includes par 3 tee shots) |
| **SG: ARG** | Strokes Gained: Around the Green — short game within 50 yards |
| **SG: P** | Strokes Gained: Putting — all shots from the green |
| **SG: T2G** | Strokes Gained: Tee to Green — OTT + APP + ARG |
| **SG: Total** | Sum of all SG categories for a hole or round |
| **LieType** | Surface the ball rests on: Tee, Fairway, Rough, Sand, Bunker, Recovery, Green |
| **Shot Chain** | Sequence of shots where each shot's end position = next shot's start position |
| **Penalty Strokes** | Additional strokes added without a physical swing (OB, hazard) |
| **Broadie Tables** | Mark Broadie's expected strokes reference data from *Every Shot Counts* |
