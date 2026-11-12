using System.Net;

namespace FairwayFinder.Landing.Profiles;

/// <summary>
/// A shields.io-style badge ("golf | 8.4 HCP · 79.2 avg") to embed in a README or portfolio, linking
/// back to the profile page. Plain SVG text, so it needs no renderer and stays sharp at any size.
/// </summary>
public static class BadgeSvg
{
    private const string Label = "⛳ golf";
    private const int Height = 20;
    private const int Padding = 6;

    public static string Render(PublicProfile profile)
    {
        var value = ProfileFormat.Summary(profile);
        var labelWidth = TextWidth(Label) + Padding * 2;
        var valueWidth = TextWidth(value) + Padding * 2;
        var width = labelWidth + valueWidth;
        var title = WebUtility.HtmlEncode($"{profile.DisplayName}: {value}");

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{Height}" role="img" aria-label="{title}">
              <title>{title}</title>
              <linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#fff" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>
              <clipPath id="r"><rect width="{width}" height="{Height}" rx="3" fill="#fff"/></clipPath>
              <g clip-path="url(#r)">
                <rect width="{labelWidth}" height="{Height}" fill="#19132e"/>
                <rect x="{labelWidth}" width="{valueWidth}" height="{Height}" fill="#6a3de7"/>
                <rect width="{width}" height="{Height}" fill="url(#s)"/>
              </g>
              <g fill="#fff" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
                <text x="{labelWidth / 2.0}" y="14" text-anchor="middle">{WebUtility.HtmlEncode(Label)}</text>
                <text x="{labelWidth + valueWidth / 2.0}" y="14" text-anchor="middle">{WebUtility.HtmlEncode(value)}</text>
              </g>
            </svg>
            """;
    }

    // Verdana 11px averages ~6.5px a character; wide glyphs (the flag emoji, "·" is narrow) adjusted.
    private static int TextWidth(string text) =>
        (int)Math.Ceiling(text.Sum(c => c switch
        {
            '⛳' => 13.0,
            '.' or ',' or '·' or ' ' or 'i' or 'l' or '1' => 4.0,
            >= 'A' and <= 'Z' => 7.5,
            _ => 6.6
        }));
}
