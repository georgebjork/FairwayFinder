using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Tests.Helpers;

public class StrokesGainedBaselineTests
{
    [Fact]
    public void Expected_strokes_from_tee_400y_returns_correct_value()
    {
        var result = StrokesGainedBaseline.GetExpectedStrokes(400, DistanceUnit.Yards, LieType.Tee);

        Assert.Equal(3.99, result, precision: 2);
    }

    [Fact]
    public void Expected_strokes_from_fairway_150y_returns_correct_value()
    {
        var result = StrokesGainedBaseline.GetExpectedStrokes(150, DistanceUnit.Yards, LieType.Fairway);

        Assert.Equal(2.99, result, precision: 2);
    }

    [Fact]
    public void Expected_strokes_interpolates_between_data_points()
    {
        // 175y fairway should interpolate between 150y (2.99) and 200y (3.18)
        var at150 = StrokesGainedBaseline.GetExpectedStrokes(150, DistanceUnit.Yards, LieType.Fairway);
        var at200 = StrokesGainedBaseline.GetExpectedStrokes(200, DistanceUnit.Yards, LieType.Fairway);
        var at175 = StrokesGainedBaseline.GetExpectedStrokes(175, DistanceUnit.Yards, LieType.Fairway);

        // 175y is an exact data point in the table (3.08), but verify it falls between neighbors
        Assert.True(at175 > at150, "175y should be greater than 150y");
        Assert.True(at175 < at200, "175y should be less than 200y");

        // Also test a non-exact point: 160y should interpolate between 150y and 175y
        var at160 = StrokesGainedBaseline.GetExpectedStrokes(160, DistanceUnit.Yards, LieType.Fairway);
        Assert.True(at160 > at150, "160y should be greater than 150y");
        Assert.True(at160 < at175, "160y should be less than 175y");
    }

    [Fact]
    public void Expected_strokes_green_20ft_returns_correct_value()
    {
        var result = StrokesGainedBaseline.GetExpectedStrokes(20, DistanceUnit.Feet, LieType.Green);

        Assert.Equal(1.87, result, precision: 2);
    }

    [Fact]
    public void Expected_strokes_holed_returns_zero()
    {
        var result = StrokesGainedBaseline.GetExpectedStrokesHoled();

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Sand_and_bunker_return_same_expected_strokes()
    {
        var sandResult = StrokesGainedBaseline.GetExpectedStrokes(100, DistanceUnit.Yards, LieType.Sand);
        var bunkerResult = StrokesGainedBaseline.GetExpectedStrokes(100, DistanceUnit.Yards, LieType.Bunker);

        Assert.Equal(sandResult, bunkerResult);
    }

    [Fact]
    public void Edge_case_very_short_distance()
    {
        // 1 yard should clamp to the minimum data point, not crash
        var result = StrokesGainedBaseline.GetExpectedStrokes(1, DistanceUnit.Yards, LieType.Fairway);

        Assert.True(result > 0, "Very short distance should return a positive value");
    }

    [Fact]
    public void Edge_case_very_long_distance()
    {
        // 700 yards should clamp to the maximum data point, not crash
        var result = StrokesGainedBaseline.GetExpectedStrokes(700, DistanceUnit.Yards, LieType.Tee);

        // Max tee value in scratch table is 600y = 4.73
        Assert.Equal(4.73, result, precision: 2);
    }
}
