using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Tests.Helpers;

public class StrokesGainedCalculatorTests
{
    #region Per-Shot Tests

    [Fact]
    public void Shot_sg_approach_to_green_positive_when_good()
    {
        // 150yd fairway -> 10ft green
        // Expected(150yd fairway) = 2.99, Expected(10ft green) = 1.61
        // SG = 2.99 - 1.61 - 1 = +0.38
        var sg = StrokesGainedCalculator.CalculateShotSg(
            startDistance: 150, startUnit: DistanceUnit.Yards, startLie: LieType.Fairway,
            endDistance: 10, endUnit: DistanceUnit.Feet, endLie: LieType.Green,
            penaltyStrokes: 0);

        Assert.True(sg > 0, "A good approach to 10ft should have positive SG");
        Assert.Equal(0.38, sg, precision: 2);
    }

    [Fact]
    public void Shot_sg_penalty_shot_returns_approximately_negative_two()
    {
        // OB from tee: 400yd tee -> 400yd tee with 1 penalty stroke
        // Expected(400yd tee) = 3.99, Expected(400yd tee) = 3.99
        // SG = 3.99 - 3.99 - (1 + 1) = -2.0
        var sg = StrokesGainedCalculator.CalculateShotSg(
            startDistance: 400, startUnit: DistanceUnit.Yards, startLie: LieType.Tee,
            endDistance: 400, endUnit: DistanceUnit.Yards, endLie: LieType.Tee,
            penaltyStrokes: 1);

        Assert.Equal(-2.0, sg, precision: 2);
    }

    [Fact]
    public void Shot_sg_holed_shot_uses_zero_expected()
    {
        // Putt from 4ft holed: Expected(4ft green) = 1.08, End = holed = 0.0
        // SG = 1.08 - 0 - 1 = +0.08
        var sg = StrokesGainedCalculator.CalculateShotSg(
            startDistance: 4, startUnit: DistanceUnit.Feet, startLie: LieType.Green,
            endDistance: null, endUnit: null, endLie: null,
            penaltyStrokes: 0);

        Assert.Equal(0.08, sg, precision: 2);
    }

    [Fact]
    public void Shot_sg_three_putt_returns_negative()
    {
        // Putt 1: 20ft -> 5ft (miss)
        // Expected(20ft) = 1.87, Expected(5ft) = 1.15
        // SG1 = 1.87 - 1.15 - 1 = -0.28
        var sg1 = StrokesGainedCalculator.CalculateShotSg(
            startDistance: 20, startUnit: DistanceUnit.Feet, startLie: LieType.Green,
            endDistance: 5, endUnit: DistanceUnit.Feet, endLie: LieType.Green,
            penaltyStrokes: 0);

        // Putt 2: 5ft -> holed
        // Expected(5ft) = 1.15, End = 0.0
        // SG2 = 1.15 - 0 - 1 = +0.15
        var sg2 = StrokesGainedCalculator.CalculateShotSg(
            startDistance: 5, startUnit: DistanceUnit.Feet, startLie: LieType.Green,
            endDistance: null, endUnit: null, endLie: null,
            penaltyStrokes: 0);

        var totalPuttingSg = sg1 + sg2;

        Assert.True(sg1 < 0, "First putt that leaves 5ft should have negative SG");
        Assert.True(totalPuttingSg < 0, "Three-putt total SG should be negative");
    }

    #endregion

    #region Classification Tests

    [Fact]
    public void Classify_tee_shot_par4_returns_off_the_tee()
    {
        var category = StrokesGainedCalculator.ClassifyShot(LieType.Tee, 400, holePar: 4);

        Assert.Equal(ShotCategory.OffTheTee, category);
    }

    [Fact]
    public void Classify_tee_shot_par3_returns_approach()
    {
        var category = StrokesGainedCalculator.ClassifyShot(LieType.Tee, 175, holePar: 3);

        Assert.Equal(ShotCategory.Approach, category);
    }

    [Fact]
    public void Classify_green_shot_returns_putting()
    {
        var category = StrokesGainedCalculator.ClassifyShot(LieType.Green, 20, holePar: 4);

        Assert.Equal(ShotCategory.Putting, category);
    }

    [Fact]
    public void Classify_30yd_rough_returns_around_the_green()
    {
        var category = StrokesGainedCalculator.ClassifyShot(LieType.Rough, 30, holePar: 4);

        Assert.Equal(ShotCategory.AroundTheGreen, category);
    }

    [Fact]
    public void Classify_100yd_fairway_returns_approach()
    {
        var category = StrokesGainedCalculator.ClassifyShot(LieType.Fairway, 100, holePar: 4);

        Assert.Equal(ShotCategory.Approach, category);
    }

    #endregion

    #region Per-Hole Tests

    [Fact]
    public void Hole_sg_categories_sum_to_total()
    {
        // Par 4, 400 yards: Drive to fairway 150yd, approach to 10ft, 2-putt
        var shots = new List<ShotData>
        {
            new() { ShotNumber = 1, StartDistance = 400, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Tee,
                     EndDistance = 150, EndDistanceUnit = DistanceUnit.Yards, EndLie = LieType.Fairway, PenaltyStrokes = 0 },
            new() { ShotNumber = 2, StartDistance = 150, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Fairway,
                     EndDistance = 10, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
            new() { ShotNumber = 3, StartDistance = 10, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green,
                     EndDistance = 2, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
            new() { ShotNumber = 4, StartDistance = 2, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green,
                     EndDistance = null, EndDistanceUnit = null, EndLie = null, PenaltyStrokes = 0 },
        };

        var result = StrokesGainedCalculator.CalculateHoleSg(shots, holePar: 4, holeNumber: 1, holeScore: 4);

        var categorySum = Math.Round(result.SgOffTheTee + result.SgApproach + result.SgAroundTheGreen + result.SgPutting, 2);
        Assert.Equal(result.SgTotal, categorySum);
    }

    [Fact]
    public void Hole_sg_with_penalty_shot_reflects_penalty()
    {
        // Par 4, 400 yards: OB tee shot (penalty), re-tee, approach to green, 2-putt = score of 6
        var shots = new List<ShotData>
        {
            new() { ShotNumber = 1, StartDistance = 400, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Tee,
                     EndDistance = 400, EndDistanceUnit = DistanceUnit.Yards, EndLie = LieType.Tee, PenaltyStrokes = 1 },
            new() { ShotNumber = 2, StartDistance = 400, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Tee,
                     EndDistance = 150, EndDistanceUnit = DistanceUnit.Yards, EndLie = LieType.Fairway, PenaltyStrokes = 0 },
            new() { ShotNumber = 3, StartDistance = 150, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Fairway,
                     EndDistance = 10, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
            new() { ShotNumber = 4, StartDistance = 10, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green,
                     EndDistance = 2, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
            new() { ShotNumber = 5, StartDistance = 2, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green,
                     EndDistance = null, EndDistanceUnit = null, EndLie = null, PenaltyStrokes = 0 },
        };

        var result = StrokesGainedCalculator.CalculateHoleSg(shots, holePar: 4, holeNumber: 1, holeScore: 6);

        // With a double bogey (6 on a par 4), total SG should be negative
        Assert.True(result.SgTotal < 0, "A double bogey with OB should have negative total SG");
        // The OB penalty shot should make OTT SG very negative
        Assert.True(result.SgOffTheTee < -1.0, "OTT SG with OB should be well below -1.0");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_shots_score_matches_shot_count_plus_penalties()
    {
        // 4 shots + 0 penalties = score of 4
        var shots = CreateValidPar4Shots();

        var errors = StrokesGainedCalculator.ValidateShots(shots, holeYardage: 400, holeScore: 4);

        Assert.Empty(errors);

        // Now test mismatch: claim score is 5 but only 4 shots + 0 penalties
        var mismatchErrors = StrokesGainedCalculator.ValidateShots(shots, holeYardage: 400, holeScore: 5);

        Assert.Contains(mismatchErrors, e => e.Contains("Score mismatch"));
    }

    [Fact]
    public void Validate_first_shot_starts_from_tee()
    {
        var shots = new List<ShotData>
        {
            new() { ShotNumber = 1, StartDistance = 150, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Fairway,
                     EndDistance = 10, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
            new() { ShotNumber = 2, StartDistance = 10, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green,
                     EndDistance = null, EndDistanceUnit = null, EndLie = null, PenaltyStrokes = 0 },
        };

        var errors = StrokesGainedCalculator.ValidateShots(shots, holeYardage: 400, holeScore: 2);

        Assert.Contains(errors, e => e.Contains("First shot must start from the tee"));
    }

    [Fact]
    public void Validate_last_shot_is_holed()
    {
        var shots = new List<ShotData>
        {
            new() { ShotNumber = 1, StartDistance = 400, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Tee,
                     EndDistance = 150, EndDistanceUnit = DistanceUnit.Yards, EndLie = LieType.Fairway, PenaltyStrokes = 0 },
            new() { ShotNumber = 2, StartDistance = 150, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Fairway,
                     EndDistance = 10, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
        };

        var errors = StrokesGainedCalculator.ValidateShots(shots, holeYardage: 400, holeScore: 2);

        Assert.Contains(errors, e => e.Contains("Last shot must be holed"));
    }

    #endregion

    #region HoleStat Derivation Tests

    [Fact]
    public void Derive_putts_from_shots()
    {
        // 2 putts on the green
        var shots = CreateValidPar4Shots();

        var (numberOfPutts, _, _, _, _, _) = StrokesGainedCalculator.DeriveHoleStatFromShots(shots, holePar: 4);

        Assert.Equal((short)2, numberOfPutts);
    }

    [Fact]
    public void Derive_fir_from_tee_shot_end_lie()
    {
        // Tee shot ends in fairway
        var shots = CreateValidPar4Shots();

        var (_, hitFairway, _, _, _, _) = StrokesGainedCalculator.DeriveHoleStatFromShots(shots, holePar: 4);

        Assert.True(hitFairway, "EndLie == Fairway on first shot should mean FIR");

        // Tee shot ends in rough
        var roughShots = new List<ShotData>
        {
            new() { ShotNumber = 1, StartDistance = 400, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Tee,
                     EndDistance = 150, EndDistanceUnit = DistanceUnit.Yards, EndLie = LieType.Rough, PenaltyStrokes = 0 },
            new() { ShotNumber = 2, StartDistance = 150, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Rough,
                     EndDistance = 10, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
            new() { ShotNumber = 3, StartDistance = 10, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green,
                     EndDistance = null, EndDistanceUnit = null, EndLie = null, PenaltyStrokes = 0 },
        };

        var (_, hitFairwayRough, _, _, _, _) = StrokesGainedCalculator.DeriveHoleStatFromShots(roughShots, holePar: 4);

        Assert.False(hitFairwayRough, "EndLie == Rough on first shot should mean missed fairway");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a valid set of shots for a par 4, 400 yard hole with score of 4:
    /// Drive to fairway 150yd, approach to 10ft, putt to 2ft, holed.
    /// </summary>
    private static List<ShotData> CreateValidPar4Shots()
    {
        return new List<ShotData>
        {
            new() { ShotNumber = 1, StartDistance = 400, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Tee,
                     EndDistance = 150, EndDistanceUnit = DistanceUnit.Yards, EndLie = LieType.Fairway, PenaltyStrokes = 0 },
            new() { ShotNumber = 2, StartDistance = 150, StartDistanceUnit = DistanceUnit.Yards, StartLie = LieType.Fairway,
                     EndDistance = 10, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
            new() { ShotNumber = 3, StartDistance = 10, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green,
                     EndDistance = 2, EndDistanceUnit = DistanceUnit.Feet, EndLie = LieType.Green, PenaltyStrokes = 0 },
            new() { ShotNumber = 4, StartDistance = 2, StartDistanceUnit = DistanceUnit.Feet, StartLie = LieType.Green,
                     EndDistance = null, EndDistanceUnit = null, EndLie = null, PenaltyStrokes = 0 },
        };
    }

    #endregion
}
