using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Helpers;
using static FairwayFinder.Features.Helpers.RoundScoringHelper;

namespace FairwayFinder.Features.Tests.Helpers;

public class RoundScoringHelperTests
{
    private static RoundStat Distribute(params (int Score, int Par)[] holes)
    {
        var stat = new RoundStat();
        ApplyScoringDistribution(stat, holes.Select((h, i) => new HoleScoreLine(i + 1, h.Score, h.Par)));
        return stat;
    }

    [Fact]
    public void Ace_on_a_par_3_is_a_hole_in_one_not_an_eagle()
    {
        var stat = Distribute((1, 3));

        Assert.Equal(1, stat.HoleInOne);
        Assert.Equal(0, stat.Eagles);
    }

    [Fact]
    public void Ace_on_a_par_4_is_a_hole_in_one_not_a_double_eagle()
    {
        var stat = Distribute((1, 4));

        Assert.Equal(1, stat.HoleInOne);
        Assert.Equal(0, stat.DoubleEagles);
    }

    [Fact]
    public void Distribution_buckets_every_other_score_by_score_minus_par()
    {
        var stat = Distribute((2, 5), (2, 4), (3, 4), (4, 4), (5, 4), (6, 4), (7, 4), (9, 4));

        Assert.Equal(0, stat.HoleInOne);
        Assert.Equal(1, stat.DoubleEagles);
        Assert.Equal(1, stat.Eagles);
        Assert.Equal(1, stat.Birdies);
        Assert.Equal(1, stat.Pars);
        Assert.Equal(1, stat.Bogies);
        Assert.Equal(1, stat.DoubleBogies);
        Assert.Equal(2, stat.TripleOrWorse);
    }

    [Fact]
    public void Distribution_agrees_with_the_per_hole_distribution_for_an_ace()
    {
        var stat = Distribute((1, 3));
        var perHole = StatsCalculator.CalculateHoleScoringDistribution(
            [new HolePlay { Score = 1 }], par: 3);

        Assert.Equal(perHole.HolesInOne, stat.HoleInOne);
        Assert.Equal(perHole.Eagles, stat.Eagles);
    }
}
