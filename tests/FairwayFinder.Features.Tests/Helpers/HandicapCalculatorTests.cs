using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Tests.Helpers;

public class HandicapCalculatorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    /// <summary>Rounds whose differentials are exactly <paramref name="differentials"/>, on slope 113.</summary>
    private static IEnumerable<HandicapCalculator.Round> RoundsWithDifferentials(params int[] differentials) =>
        differentials.Select((d, i) => new HandicapCalculator.Round(Start.AddDays(i), 70 + d, 70m, 113));

    [Fact]
    public void Differential_scales_by_slope_and_rounds_to_one_decimal()
    {
        // 113 / 130 × (90 − 71.5) = 16.08 → 16.1
        Assert.Equal(16.1m, HandicapCalculator.Differential(90, 71.5m, 130));
        Assert.Equal(10.0m, HandicapCalculator.Differential(82, 72m, 113));
    }

    [Fact]
    public void Fewer_than_three_rounds_has_no_index()
    {
        Assert.Null(HandicapCalculator.Calculate([]));
        Assert.Null(HandicapCalculator.Calculate(RoundsWithDifferentials(10, 12)));
    }

    [Theory]
    [InlineData(3, 1, -2.0)]
    [InlineData(4, 1, -1.0)]
    [InlineData(5, 1, 0)]
    [InlineData(6, 2, -1.0)]
    [InlineData(7, 2, 0)]
    [InlineData(8, 2, 0)]
    [InlineData(9, 3, 0)]
    [InlineData(11, 3, 0)]
    [InlineData(12, 4, 0)]
    [InlineData(14, 4, 0)]
    [InlineData(15, 5, 0)]
    [InlineData(16, 5, 0)]
    [InlineData(17, 6, 0)]
    [InlineData(18, 6, 0)]
    [InlineData(19, 7, 0)]
    [InlineData(20, 8, 0)]
    public void Follows_the_whs_table(int count, int use, double adjustment)
    {
        Assert.Equal((use, (decimal)adjustment), HandicapCalculator.Table(count));
    }

    [Fact]
    public void Averages_the_lowest_differentials_with_the_adjustment()
    {
        // Six rounds: the lowest two (8, 10) average 9.0, minus 1.0.
        Assert.Equal(8.0m, HandicapCalculator.Calculate(RoundsWithDifferentials(20, 8, 15, 10, 18, 30)));

        // Twenty rounds: the lowest eight of 1..20 average 4.5.
        Assert.Equal(4.5m, HandicapCalculator.Calculate(RoundsWithDifferentials(Enumerable.Range(1, 20).ToArray())));
    }

    [Fact]
    public void Only_the_most_recent_twenty_rounds_count()
    {
        // Five excellent old rounds, then twenty at 20: the old ones fall out of the window.
        var rounds = RoundsWithDifferentials([0, 0, 0, 0, 0, .. Enumerable.Repeat(20, 20)]);

        Assert.Equal(20.0m, HandicapCalculator.Calculate(rounds));
    }

    [Fact]
    public void Truncates_rather_than_rounds()
    {
        // Slope 130 gives real decimals: differentials 10.9, 11.7, 12.6 → lowest 10.9 − 2.0 = 8.9.
        var rounds = new[] { 84, 85, 86 }.Select((s, i) => new HandicapCalculator.Round(Start.AddDays(i), s, 71.5m, 130));
        Assert.Equal(8.9m, HandicapCalculator.Calculate(rounds));

        // Seven rounds: the lowest two, 10.0 and 10.5, average 10.25 → 10.2, not 10.3.
        Assert.Equal(10.2m, HandicapCalculator.Calculate(
            new[] { 10.0m, 10.5m, 15m, 15m, 15m, 15m, 15m }.Select((d, i) => new HandicapCalculator.Round(Start.AddDays(i), 0, -d, 113))));
    }

    [Fact]
    public void Caps_at_54()
    {
        Assert.Equal(54.0m, HandicapCalculator.Calculate(RoundsWithDifferentials(70, 70, 70, 70, 70)));
    }

    [Fact]
    public void Ignores_rounds_without_a_slope()
    {
        var rounds = RoundsWithDifferentials(10, 10, 10).Append(new HandicapCalculator.Round(Start.AddYears(1), 80, 0m, 0));

        Assert.Equal(8.0m, HandicapCalculator.Calculate(rounds));
    }
}
