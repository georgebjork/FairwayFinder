using FairwayFinder.Features.Data;

namespace FairwayFinder.Admin.Components.Pages.Stats.Components;

/// <summary>Which way a metric has to move for the golfer to have improved.</summary>
public enum Better { Higher, Lower, Neutral }

/// <summary>How a change should read: an improvement, a regression, or neither.</summary>
public enum ChangeTone { Good, Bad, Neutral }

/// <summary>
/// One metric across several years. <see cref="Values"/> is one pre-formatted cell per year,
/// oldest to newest; <see cref="Change"/> is newest minus oldest, null when either end is missing.
/// </summary>
public sealed record ComparisonRow(string Metric, IReadOnlyList<string> Values, double? Change, string ChangeText, Better Better)
{
    public ChangeTone Tone
    {
        get
        {
            if (Change is not { } change || Better == Better.Neutral || Math.Abs(change) < 1e-9)
                return ChangeTone.Neutral;
            var improved = Better == Better.Higher ? change > 0 : change < 0;
            return improved ? ChangeTone.Good : ChangeTone.Bad;
        }
    }
}

/// <summary>
/// Turns one <see cref="UserStatsResponse"/> per year into comparison rows for the stats
/// diagnostics "Compare years" mode. Pure mapping so the grid stays dumb and the
/// better/worse rules are testable.
/// </summary>
public static class YearComparisonModel
{
    private const string Sg = "+0.00;-0.00;0.00";
    private const string Score = "0.0";
    private const string ScoreChange = "+0.0;-0.0;0.0";
    private const string Avg = "0.00";
    private const string AvgChange = "+0.00;-0.00;0.00";
    private const string Count = "0";
    private const string CountChange = "+0;-0;0";

    public static List<ComparisonRow> StrokesGained(IReadOnlyList<UserStatsResponse> years) =>
    [
        Row("Off the tee", years, s => s.StrokesGained.Summary?.SgOffTheTee, Sg, Sg, Better.Higher),
        Row("Approach", years, s => s.StrokesGained.Summary?.SgApproach, Sg, Sg, Better.Higher),
        Row("Around the green", years, s => s.StrokesGained.Summary?.SgAroundTheGreen, Sg, Sg, Better.Higher),
        Row("Putting", years, s => s.StrokesGained.Summary?.SgPutting, Sg, Sg, Better.Higher),
        Row("Tee to green", years, s => s.StrokesGained.Summary?.SgTeeToGreen, Sg, Sg, Better.Higher),
        Row("Total", years, s => s.StrokesGained.Summary?.SgTotal, Sg, Sg, Better.Higher)
    ];

    public static List<ComparisonRow> Scoring(IReadOnlyList<UserStatsResponse> years) =>
    [
        Row("18-hole rounds", years, s => s.Scoring.Rounds18Hole, Count, CountChange, Better.Neutral),
        Row("9-hole rounds", years, s => s.Scoring.Rounds9Hole, Count, CountChange, Better.Neutral),
        Row("Avg 18-hole score", years, s => s.Scoring.Average18HoleScore, Score, ScoreChange, Better.Lower),
        Row("Avg 9-hole score", years, s => s.Scoring.Average9HoleScore, Score, ScoreChange, Better.Lower),
        Row("Best 18", years, s => s.Scoring.Best18HoleRound?.Score, Count, CountChange, Better.Lower),
        Row("Best 9", years, s => s.Scoring.Best9HoleRound?.Score, Count, CountChange, Better.Lower)
    ];

    public static List<ComparisonRow> BallStriking(IReadOnlyList<UserStatsResponse> years) =>
    [
        Row("Hole-tracked rounds", years, s => s.BallStriking.RoundsIncluded, Count, CountChange, Better.Neutral),
        PctRow("FIR%", years, s => s.BallStriking.FirPercent, Better.Higher),
        PctRow("GIR%", years, s => s.BallStriking.GirPercent, Better.Higher),
        PctRow("Tee penalty%", years, s => s.BallStriking.TeePenaltyPercent, Better.Lower)
    ];

    public static List<ComparisonRow> ShortGame(IReadOnlyList<UserStatsResponse> years) =>
    [
        PctRow("Up and down%", years, s => s.ShortGame.UpAndDownPercent, Better.Higher),
        Row("Avg putts (18)", years, s => s.ShortGame.Average18HolePutts, Avg, AvgChange, Better.Lower),
        Row("Avg putts (9)", years, s => s.ShortGame.Average9HolePutts, Avg, AvgChange, Better.Lower),
        Row("Avg 3-putts (18)", years, s => s.ShortGame.Average18HoleThreePutts, Avg, AvgChange, Better.Lower),
        Row("Avg 3-putts (9)", years, s => s.ShortGame.Average9HoleThreePutts, Avg, AvgChange, Better.Lower)
    ];

    /// <summary>
    /// Par-type averages and the scoring distribution as a share of holes played. Raw counts
    /// would mostly measure how much golf was played that year, so they are not compared.
    /// </summary>
    public static List<ComparisonRow> Distribution(IReadOnlyList<UserStatsResponse> years) =>
    [
        Row("Par 3 avg", years, s => s.ParTypeScoring.Par3Average, Avg, AvgChange, Better.Lower),
        Row("Par 4 avg", years, s => s.ParTypeScoring.Par4Average, Avg, AvgChange, Better.Lower),
        Row("Par 5 avg", years, s => s.ParTypeScoring.Par5Average, Avg, AvgChange, Better.Lower),
        Row("Holes played", years, s => s.ScoringDistribution.TotalHoles, Count, CountChange, Better.Neutral),
        PctRow("Birdie or better", years, s => Share(s.ScoringDistribution, d => d.HolesInOne + d.DoubleEagles + d.Eagles + d.Birdies), Better.Higher),
        PctRow("Par", years, s => Share(s.ScoringDistribution, d => d.Pars), Better.Higher),
        PctRow("Bogey", years, s => Share(s.ScoringDistribution, d => d.Bogeys), Better.Lower),
        PctRow("Double or worse", years, s => Share(s.ScoringDistribution, d => d.DoubleBogeys + d.TripleOrWorse), Better.Lower)
    ];

    /// <summary>Percentage of holes played that fall in a bucket; null when no holes were played.</summary>
    public static double? Share(ScoringDistribution d, Func<ScoringDistribution, int> bucket) =>
        d.TotalHoles == 0 ? null : 100.0 * bucket(d) / d.TotalHoles;

    private static ComparisonRow PctRow(string metric, IReadOnlyList<UserStatsResponse> years,
        Func<UserStatsResponse, double?> pick, Better better)
    {
        var values = years.Select(pick).ToList();
        var change = ChangeOf(values);
        return new ComparisonRow(metric,
            values.Select(StatFormat.Pct).ToList(),
            change,
            change.HasValue ? $"{change.Value.ToString(ScoreChange)} pts" : "—",
            better);
    }

    private static ComparisonRow Row(string metric, IReadOnlyList<UserStatsResponse> years,
        Func<UserStatsResponse, double?> pick, string format, string changeFormat, Better better)
    {
        var values = years.Select(pick).ToList();
        var change = ChangeOf(values);
        return new ComparisonRow(metric,
            values.Select(v => StatFormat.Num(v, format)).ToList(),
            change,
            StatFormat.Num(change, changeFormat),
            better);
    }

    private static double? ChangeOf(IReadOnlyList<double?> values) =>
        values.Count >= 2 && values[0] is { } first && values[^1] is { } last ? last - first : null;
}

/// <summary>Number formatting shared by the stats diagnostics tables.</summary>
public static class StatFormat
{
    public static string Signed(double value) => value.ToString("+0.00;-0.00;0.00");
    public static string Pct(double? value) => value.HasValue ? $"{value.Value:0.0}%" : "—";
    public static string Num(double? value, string format = "0.00") => value.HasValue ? value.Value.ToString(format) : "—";
}
