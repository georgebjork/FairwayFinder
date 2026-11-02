using FairwayFinder.Admin.Components.Pages.Stats.Components;
using FairwayFinder.Features.Data;

namespace FairwayFinder.Admin.IntegrationTests.Pages;

/// <summary>
/// The stats "Compare years" grids render whatever <see cref="YearComparisonModel"/> says, so this
/// is where newest-minus-oldest and the better/worse colouring are decided. Pure mapping, no host.
/// </summary>
public class YearComparisonModelTests
{
    [Fact]
    public void Change_is_newest_minus_oldest_and_ignores_the_middle_year()
    {
        var rows = YearComparisonModel.Scoring([Year(avg18: 92.0), Year(avg18: 80.0), Year(avg18: 88.5)]);
        var avg = rows.Single(r => r.Metric == "Avg 18-hole score");

        Assert.Equal(["92.0", "80.0", "88.5"], avg.Values);
        Assert.Equal(-3.5, avg.Change!.Value, 6);
        Assert.Equal("-3.5", avg.ChangeText);
    }

    [Fact]
    public void Lower_score_reads_as_an_improvement_and_lower_gir_as_a_regression()
    {
        var years = new[] { Year(avg18: 90, gir: 40), Year(avg18: 85, gir: 30) };

        var score = YearComparisonModel.Scoring(years).Single(r => r.Metric == "Avg 18-hole score");
        var gir = YearComparisonModel.BallStriking(years).Single(r => r.Metric == "GIR%");

        Assert.Equal(ChangeTone.Good, score.Tone);
        Assert.Equal(ChangeTone.Bad, gir.Tone);
        Assert.Equal("-10.0 pts", gir.ChangeText);
    }

    [Fact]
    public void A_year_without_shot_tracking_has_no_strokes_gained_change()
    {
        var rows = YearComparisonModel.StrokesGained([Year(), Year(sgTotal: 1.25)]);
        var total = rows.Single(r => r.Metric == "Total");

        Assert.Equal(["—", "+1.25"], total.Values);
        Assert.Null(total.Change);
        Assert.Equal("—", total.ChangeText);
        Assert.Equal(ChangeTone.Neutral, total.Tone);
    }

    [Fact]
    public void Round_counts_are_never_coloured()
    {
        var rows = YearComparisonModel.Scoring([Year(rounds18: 5), Year(rounds18: 20)]);
        var count = rows.Single(r => r.Metric == "18-hole rounds");

        Assert.Equal(15, count.Change);
        Assert.Equal(ChangeTone.Neutral, count.Tone);
    }

    [Fact]
    public void Distribution_compares_share_of_holes_not_raw_counts()
    {
        // 9 birdies in 90 holes (10%) vs 18 in 360 (5%): more birdies, but a worse rate.
        var rows = YearComparisonModel.Distribution([
            Year(distribution: new ScoringDistribution { Birdies = 9, Pars = 81 }),
            Year(distribution: new ScoringDistribution { Birdies = 18, Pars = 342 })
        ]);
        var birdies = rows.Single(r => r.Metric == "Birdie or better");

        Assert.Equal(["10.0%", "5.0%"], birdies.Values);
        Assert.Equal(-5.0, birdies.Change!.Value, 6);
        Assert.Equal(ChangeTone.Bad, birdies.Tone);
    }

    [Fact]
    public void Share_is_null_when_no_holes_were_played()
    {
        Assert.Null(YearComparisonModel.Share(new ScoringDistribution(), d => d.Birdies));
    }

    private static UserStatsResponse Year(
        double? avg18 = null, double? gir = null, double? sgTotal = null, int rounds18 = 0,
        ScoringDistribution? distribution = null) => new()
    {
        Scoring = new ScoringStats { Average18HoleScore = avg18, Rounds18Hole = rounds18 },
        BallStriking = new BallStrikingStats { GirPercent = gir },
        StrokesGained = new StrokesGainedStats
        {
            Summary = sgTotal is null ? null : new StrokesGainedSummary { SgTotal = sgTotal.Value }
        },
        ScoringDistribution = distribution ?? new ScoringDistribution()
    };
}
