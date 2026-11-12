namespace FairwayFinder.Features.Helpers;

/// <summary>
/// An <b>estimated</b> handicap index in the shape of the World Handicap System: score differentials
/// from course rating and slope, then the WHS table for how many of the most recent 20 to average.
/// </summary>
/// <remarks>
/// Deliberately simpler than an official index, and labelled "est." wherever it is shown:
/// <list type="bullet">
/// <item>Gross score, not adjusted gross score: no net-double-bogey cap per hole.</item>
/// <item>18-hole rounds only; 9-hole rounds are not combined.</item>
/// <item>No playing-conditions calculation, and no soft or hard cap.</item>
/// </list>
/// </remarks>
public static class HandicapCalculator
{
    public const int WindowSize = 20;
    public const decimal Maximum = 54.0m;

    /// <summary>One 18-hole round's inputs.</summary>
    public readonly record struct Round(DateOnly DatePlayed, int Score, decimal Rating, int Slope);

    /// <summary>(113 / slope) × (score − rating), rounded to one decimal as WHS does.</summary>
    public static decimal Differential(int score, decimal rating, int slope) =>
        Math.Round(113m / slope * (score - rating), 1, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The estimate from the most recent <see cref="WindowSize"/> rounds, or null with fewer than 3.
    /// Rounds with a non-positive slope (missing teebox data) are ignored.
    /// </summary>
    public static decimal? Calculate(IEnumerable<Round> rounds)
    {
        var differentials = rounds
            .Where(r => r.Slope > 0)
            .OrderByDescending(r => r.DatePlayed)
            .Take(WindowSize)
            .Select(r => Differential(r.Score, r.Rating, r.Slope))
            .ToList();

        if (differentials.Count < 3)
            return null;

        var (use, adjustment) = Table(differentials.Count);
        var average = differentials.Order().Take(use).Average() + adjustment;

        // WHS truncates rather than rounds the index to one decimal.
        var index = Math.Truncate(average * 10) / 10;
        return Math.Min(index, Maximum);
    }

    /// <summary>WHS Rule 5.2a: how many of the lowest differentials to average, and the adjustment.</summary>
    public static (int Use, decimal Adjustment) Table(int count) => count switch
    {
        3 => (1, -2.0m),
        4 => (1, -1.0m),
        5 => (1, 0m),
        6 => (2, -1.0m),
        7 or 8 => (2, 0m),
        >= 9 and <= 11 => (3, 0m),
        >= 12 and <= 14 => (4, 0m),
        15 or 16 => (5, 0m),
        17 or 18 => (6, 0m),
        19 => (7, 0m),
        >= 20 => (8, 0m),
        _ => throw new ArgumentOutOfRangeException(nameof(count), count, "At least 3 differentials are needed.")
    };
}
