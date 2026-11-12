using SkiaSharp;

namespace FairwayFinder.Landing.Profiles;

/// <summary>
/// Renders the 1200×630 PNG that LinkedIn, Slack and iMessage show when the profile link is pasted.
/// Crawlers don't run JavaScript or accept SVG for <c>og:image</c>, so this has to be a real bitmap.
/// </summary>
/// <remarks>
/// Text uses the bundled Inter (<c>Fonts/Inter.ttf</c>, SIL OFL) rather than a system typeface:
/// SkiaSharp's Linux NoDependencies build, which App Platform's build runs on, has no fontconfig and
/// so no default font, and would draw every label as nothing.
/// </remarks>
public static class OgCardRenderer
{
    public const int Width = 1200;
    public const int Height = 630;

    private const float Margin = 72;

    // site.css tokens, as colours.
    private static readonly SKColor Ink = SKColor.Parse("#0f0b1d");
    private static readonly SKColor Brand = SKColor.Parse("#6a3de7");
    private static readonly SKColor Text = SKColor.Parse("#f2effa");
    private static readonly SKColor TextMuted = SKColor.Parse("#b3abc9");
    private static readonly SKColor Green = SKColor.Parse("#3ecf8e");
    private static readonly SKColor Line = new(255, 255, 255, 26);

    public static byte[] Render(PublicProfile profile, string assetsDir)
    {
        using var typeface = SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Fonts", "Inter.ttf"))
                             ?? throw new InvalidOperationException("Fonts/Inter.ttf is missing from the build output.");

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        DrawBackground(canvas);
        DrawWordmark(canvas, Path.Combine(assetsDir, "img", "wordmark-white.png"));

        using var nameFont = new SKFont(typeface, 64) { Embolden = true };
        using var subFont = new SKFont(typeface, 28);
        using var valueFont = new SKFont(typeface, 96) { Embolden = true };
        using var labelFont = new SKFont(typeface, 24);

        using var textPaint = new SKPaint { Color = Text, IsAntialias = true };
        using var mutedPaint = new SKPaint { Color = TextMuted, IsAntialias = true };

        canvas.DrawText(profile.DisplayName, Margin, 228, SKTextAlign.Left, nameFont, textPaint);
        canvas.DrawText($"Golf stats · fairwayfinder.pro/golf/{profile.Slug}", Margin, 276, SKTextAlign.Left, subFont, mutedPaint);

        var stats = Headline(profile);
        var columnWidth = (Width - Margin * 2) / stats.Count;
        for (var i = 0; i < stats.Count; i++)
        {
            var x = Margin + i * columnWidth;
            canvas.DrawText(stats[i].Value, x, 420, SKTextAlign.Left, valueFont, textPaint);
            canvas.DrawText(stats[i].Label, x, 462, SKTextAlign.Left, labelFont, mutedPaint);
        }

        DrawSparkline(canvas, profile.Trend.Select(t => t.Score).ToList(), new SKRect(Margin, 510, Width - Margin, 582));

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Three headline numbers; best round stands in when there's no handicap yet.</summary>
    private static List<(string Value, string Label)> Headline(PublicProfile p)
    {
        var h = p.Headline;
        var stats = new List<(string, string)>();

        if (h.HandicapIndex is not null)
            stats.Add((ProfileFormat.Handicap(h.HandicapIndex), "Handicap (est.)"));
        else if (h.BestRound is not null)
            stats.Add((h.BestRound.Score.ToString(), "Best round"));

        stats.Add((ProfileFormat.Average(h.ScoringAverage18), "Scoring average"));
        stats.Add((h.RoundsThisYear.ToString(), $"Rounds in {p.GeneratedOn.Year}"));
        return stats;
    }

    private static void DrawBackground(SKCanvas canvas)
    {
        canvas.Clear(Ink);

        // The hero's purple glow from the top right, as on the landing page.
        using var glow = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(Width * 0.85f, 0), Width * 0.75f,
                [Brand.WithAlpha(140), Brand.WithAlpha(0)],
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, Width, Height, glow);
    }

    private static void DrawWordmark(SKCanvas canvas, string path)
    {
        if (!File.Exists(path)) return;

        using var image = SKImage.FromEncodedData(path);
        if (image is null) return;

        const float width = 220;
        var height = width * image.Height / image.Width;
        canvas.DrawImage(image, SKRect.Create(Margin, 64, width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
    }

    /// <summary>Recent 18-hole scores, oldest on the left; the same orientation as the page's trend chart.</summary>
    private static void DrawSparkline(SKCanvas canvas, List<int> scores, SKRect area)
    {
        using var baseline = new SKPaint { Color = Line, StrokeWidth = 2, IsAntialias = true };
        canvas.DrawLine(area.Left, area.Bottom, area.Right, area.Bottom, baseline);

        if (scores.Count < 2) return;

        var min = scores.Min();
        var max = Math.Max(scores.Max(), min + 1);

        using var builder = new SKPathBuilder();
        for (var i = 0; i < scores.Count; i++)
        {
            var x = area.Left + area.Width * i / (scores.Count - 1);
            var y = area.Bottom - area.Height * (scores[i] - min) / (max - min);
            if (i == 0) builder.MoveTo(x, y); else builder.LineTo(x, y);
        }
        using var path = builder.Detach();

        using var stroke = new SKPaint
        {
            Color = Green,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        canvas.DrawPath(path, stroke);
    }
}
