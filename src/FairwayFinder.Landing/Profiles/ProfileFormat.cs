using System.Globalization;

namespace FairwayFinder.Landing.Profiles;

/// <summary>How profile numbers read, shared by the page, the badge and the OG card.</summary>
public static class ProfileFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string PageUrl(string slug) => $"{Site.Url}/golf/{slug}/";

    /// <summary>Site-relative link to a round's card, from anywhere on the site.</summary>
    public static string RoundPath(string slug, string key) => $"/golf/{slug}/rounds/{key}/";

    /// <summary>WHS writes a plus handicap as "+1.2"; the index is stored negative.</summary>
    public static string Handicap(decimal? index) => index switch
    {
        null => "–",
        < 0 => "+" + (-index.Value).ToString("0.0", Invariant),
        _ => index.Value.ToString("0.0", Invariant)
    };

    public static string Average(double? average) => average?.ToString("0.0", Invariant) ?? "–";

    public static string ToPar(int toPar) => toPar switch
    {
        0 => "E",
        > 0 => "+" + toPar.ToString(Invariant),
        _ => toPar.ToString(Invariant)
    };

    /// <summary>Strokes gained always carries its sign: "+0.42", "−1.10".</summary>
    public static string Sg(double value) =>
        (value >= 0 ? "+" : "−") + Math.Abs(value).ToString("0.00", Invariant);

    public static string Date(DateOnly date) => date.ToString("MMM d, yyyy", Invariant);

    public static string ShortDate(DateOnly date) => date.ToString("MMM d", Invariant);

    /// <summary>The badge and the og:description: "8.4 HCP · 79.2 avg".</summary>
    public static string Summary(PublicProfile p)
    {
        var parts = new List<string>();
        if (p.Headline.HandicapIndex is not null) parts.Add($"{Handicap(p.Headline.HandicapIndex)} HCP");
        if (p.Headline.ScoringAverage18 is not null) parts.Add($"{Average(p.Headline.ScoringAverage18)} avg");
        if (parts.Count == 0) parts.Add($"{p.Headline.TotalRounds} rounds");
        return string.Join(" · ", parts);
    }

    public static string Description(PublicProfile p) =>
        $"{p.DisplayName}'s golf stats: {Summary(p)} · {p.Headline.RoundsThisYear} rounds in {p.GeneratedOn.Year}. " +
        "Tracked hole by hole with FairwayFinder.";
}
