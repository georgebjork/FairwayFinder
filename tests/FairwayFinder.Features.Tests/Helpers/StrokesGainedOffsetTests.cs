using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Tests.Helpers;

/// <summary>
/// Tests the golfer-level offset applied on top of the raw strokes-gained benchmark.
/// Offsets are 18-hole per-category totals, distributed per hole and added to computed SG.
/// </summary>
public class StrokesGainedOffsetTests
{
    [Fact]
    public void Tour_level_has_zero_offset()
    {
        var offsets = StrokesGainedBaseline.GetRoundOffsets(BaselineLevel.Tour);
        Assert.Equal(new SgCategoryOffsets(0, 0, 0, 0), offsets);
    }

    [Fact]
    public void Scratch_and_hcp25_offsets_match_reference_chart()
    {
        Assert.Equal(new SgCategoryOffsets(1.78, 2.03, 0.39, 0.94), StrokesGainedBaseline.GetRoundOffsets(BaselineLevel.Scratch));
        Assert.Equal(new SgCategoryOffsets(7.53, 14.23, 4.71, 4.71), StrokesGainedBaseline.GetRoundOffsets(BaselineLevel.Hcp25));
    }

    [Fact]
    public void Higher_handicap_levels_add_larger_offsets_to_hole_sg()
    {
        var tour = StrokesGainedCalculator.CalculateHoleSg(Par4Shots(), holePar: 4, holeNumber: 1, holeScore: 4, level: BaselineLevel.Tour);
        var scratch = StrokesGainedCalculator.CalculateHoleSg(Par4Shots(), holePar: 4, holeNumber: 1, holeScore: 4, level: BaselineLevel.Scratch);
        var hcp25 = StrokesGainedCalculator.CalculateHoleSg(Par4Shots(), holePar: 4, holeNumber: 1, holeScore: 4, level: BaselineLevel.Hcp25);

        // Offsets are positive and grow with handicap, so total SG shifts upward.
        Assert.True(scratch.SgTotal > tour.SgTotal, "Scratch adds a positive offset over Tour (raw)");
        Assert.True(hcp25.SgTotal > scratch.SgTotal, "25-handicap offset exceeds scratch");

        // Categories move up too (or stay equal within rounding), never down.
        Assert.True(scratch.SgOffTheTee >= tour.SgOffTheTee);
        Assert.True(scratch.SgPutting >= tour.SgPutting);
    }

    [Fact]
    public void Offset_scales_linearly_with_holes_played()
    {
        // Identical holes, so the per-hole offset delta is constant: an 18-hole round's total
        // level adjustment is exactly twice a 9-hole round's.
        var nine = RoundWith(9);
        var eighteen = RoundWith(18);

        var nineDelta = StrokesGainedCalculator.CalculateRoundSg(nine, BaselineLevel.Scratch).SgTotal
                        - StrokesGainedCalculator.CalculateRoundSg(nine, BaselineLevel.Tour).SgTotal;
        var eighteenDelta = StrokesGainedCalculator.CalculateRoundSg(eighteen, BaselineLevel.Scratch).SgTotal
                            - StrokesGainedCalculator.CalculateRoundSg(eighteen, BaselineLevel.Tour).SgTotal;

        Assert.Equal(2 * nineDelta, eighteenDelta, precision: 2);

        // A full 18-hole round applies ~the whole scratch offset (1.78 + 2.03 + 0.39 + 0.94 = 5.14;
        // slightly under after per-hole rounding).
        Assert.InRange(eighteenDelta, 4.9, 5.2);
    }

    private static List<ShotData> Par4Shots() => new()
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

    private static RoundResponse RoundWith(int holeCount)
    {
        var round = new RoundResponse { UsingShotTracking = true };
        for (var i = 1; i <= holeCount; i++)
        {
            round.Holes.Add(new RoundHole
            {
                HoleNumber = i,
                Par = 4,
                Score = 4,
                Shots = Par4Shots()
            });
        }
        return round;
    }
}
