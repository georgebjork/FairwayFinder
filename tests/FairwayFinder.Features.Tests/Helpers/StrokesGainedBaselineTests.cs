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

        Assert.Equal(2.94, result, precision: 2);
    }

    [Fact]
    public void Expected_strokes_values_increase_with_distance()
    {
        var at150 = StrokesGainedBaseline.GetExpectedStrokes(150, DistanceUnit.Yards, LieType.Fairway);
        var at175 = StrokesGainedBaseline.GetExpectedStrokes(175, DistanceUnit.Yards, LieType.Fairway);
        var at200 = StrokesGainedBaseline.GetExpectedStrokes(200, DistanceUnit.Yards, LieType.Fairway);

        Assert.True(at175 > at150, "175y should be greater than 150y");
        Assert.True(at200 > at175, "200y should be greater than 175y");
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
    public void Sand_returns_expected_strokes()
    {
        var result = StrokesGainedBaseline.GetExpectedStrokes(100, DistanceUnit.Yards, LieType.Sand);

        // Sand at 100y from CSV = 3.23
        Assert.Equal(3.23, result, precision: 2);
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

        // Max tee value from CSV at 600y = 4.82
        Assert.Equal(4.82, result, precision: 2);
    }

    [Fact]
    public void Green_every_foot_has_data()
    {
        // Verify we have data for every foot 1-90
        for (int feet = 1; feet <= 90; feet++)
        {
            var result = StrokesGainedBaseline.GetExpectedStrokes(feet, DistanceUnit.Feet, LieType.Green);
            Assert.True(result >= 1.0 && result <= 2.5, $"Green at {feet}ft should be between 1.0 and 2.5, got {result}");
        }
    }

}
