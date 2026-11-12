namespace FairwayFinder.Features.Data;

/// <summary>
/// A golfer's opt-in public stats page, as served anonymously by <c>GET /api/public/profiles/{slug}</c>
/// and baked into <c>fairwayfinder.pro/golf/{slug}</c> by the Landing build.
/// </summary>
/// <remarks>
/// Anonymous, so it carries nothing that identifies the account: no user id, email, username or
/// public identifier. Sections the golfer turned off are null; with course names off, every
/// <c>CourseName</c> is null. Only completed rounds not excluded from stats count.
/// </remarks>
public class PublicProfileResponse
{
    public string DisplayName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    /// <summary>When these numbers were computed (UTC).</summary>
    public DateTime GeneratedOn { get; set; }

    public PublicProfileHeadline Headline { get; set; } = new();

    /// <summary>Null when the golfer hid it, or when no round has shot tracking.</summary>
    public PublicStrokesGained? StrokesGained { get; set; }

    public PublicScoringDistribution ScoringDistribution { get; set; } = new();

    /// <summary>The most recent 18-hole rounds, oldest first, for the trend chart.</summary>
    public List<PublicTrendPoint> Trend { get; set; } = [];

    /// <summary>Most recent first. Null when the golfer hid it.</summary>
    public List<PublicRecentRound>? RecentRounds { get; set; }

    /// <summary>
    /// Hole-by-hole cards for the rounds the page links to: the recent rounds and the best round.
    /// Empty when the golfer hid recent rounds. Each one's <c>Key</c> is its URL segment.
    /// </summary>
    public List<PublicScorecard> Scorecards { get; set; } = [];
}

public class PublicProfileHeadline
{
    /// <summary>Estimated, from 18-hole rounds (see <c>HandicapCalculator</c>). Null with fewer than 3.</summary>
    public decimal? HandicapIndex { get; set; }

    public double? ScoringAverage18 { get; set; }
    public int RoundsThisYear { get; set; }
    public int TotalRounds { get; set; }
    public PublicBestRound? BestRound { get; set; }
}

public class PublicBestRound
{
    /// <summary>The round's scorecard key, or null when it has no published card.</summary>
    public string? Key { get; set; }
    public int Score { get; set; }
    public int ToPar { get; set; }
    public DateOnly DatePlayed { get; set; }
    public string? CourseName { get; set; }
}

public class PublicStrokesGained
{
    public double OffTheTee { get; set; }
    public double Approach { get; set; }
    public double AroundTheGreen { get; set; }
    public double Putting { get; set; }
    public double Total { get; set; }
    public int RoundsIncluded { get; set; }
}

public class PublicScoringDistribution
{
    /// <summary>Eagles, double eagles and holes in one together.</summary>
    public int EaglesOrBetter { get; set; }
    public int Birdies { get; set; }
    public int Pars { get; set; }
    public int Bogeys { get; set; }
    public int DoubleBogeys { get; set; }
    public int TripleOrWorse { get; set; }
    public int TotalHoles { get; set; }
}

public class PublicTrendPoint
{
    public DateOnly DatePlayed { get; set; }
    public int Score { get; set; }
    public string? CourseName { get; set; }
}

public class PublicRecentRound
{
    /// <summary>The round's scorecard key (see <see cref="PublicScorecard.Key"/>).</summary>
    public string? Key { get; set; }
    public DateOnly DatePlayed { get; set; }
    public int Score { get; set; }
    public int ToPar { get; set; }
    public bool FullRound { get; set; }
    public string? CourseName { get; set; }
}

/// <summary>One round's scorecard on the public page.</summary>
public class PublicScorecard
{
    /// <summary>
    /// URL segment: the date played (<c>2026-09-21</c>), with <c>-2</c>, <c>-3</c>… for later rounds the
    /// same day. Never the round id, and stable across rebuilds.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public DateOnly DatePlayed { get; set; }
    public string? CourseName { get; set; }
    public string TeeboxName { get; set; } = string.Empty;

    /// <summary>The teebox's course rating and slope, shown in the card's handicap row.</summary>
    public decimal Rating { get; set; }
    public int Slope { get; set; }

    public int Score { get; set; }
    public int ToPar { get; set; }

    /// <summary>Par of the holes played.</summary>
    public int Par { get; set; }

    public bool FullRound { get; set; }

    /// <summary>The holes played, in order.</summary>
    public List<PublicScorecardHole> Holes { get; set; } = [];

    /// <summary>Null when the round didn't track hole stats.</summary>
    public int? FairwaysHit { get; set; }
    public int? FairwaysTotal { get; set; }
    public int? GreensHit { get; set; }
    public int? GreensTotal { get; set; }
    public int? Putts { get; set; }

    /// <summary>This round's strokes gained. Null without shot tracking, or when the golfer hid it.</summary>
    public PublicStrokesGained? StrokesGained { get; set; }
}

public class PublicScorecardHole
{
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int Yardage { get; set; }

    /// <summary>The hole's stroke index on this teebox (1 = hardest).</summary>
    public int Handicap { get; set; }

    public int Score { get; set; }

    /// <summary>Null when not tracked; fairway is also null on par 3s.</summary>
    public int? Putts { get; set; }
    public bool? Fairway { get; set; }
    public bool? Green { get; set; }

    /// <summary>
    /// Which way a missed fairway or green went: <c>Left</c>, <c>Right</c>, <c>Short</c> or <c>Long</c>.
    /// Null when hit, untracked, or the direction wasn't recorded.
    /// </summary>
    public string? FairwayMiss { get; set; }
    public string? GreenMiss { get; set; }
}

/// <summary>A golfer's public-page settings, as sent by the iOS app or the admin console.</summary>
public class PublicProfileSettingsRequest
{
    public bool IsPublic { get; set; }

    /// <summary>Null or blank keeps the current slug, or derives one from the username the first time.</summary>
    public string? Slug { get; set; }

    public bool ShowStrokesGained { get; set; } = true;
    public bool ShowRecentRounds { get; set; } = true;
    public bool ShowCourseNames { get; set; } = true;
}

public enum PublicProfileSettingsStatus
{
    Ok,
    Invalid,
    Taken,
    NotFound
}

public record PublicProfileSettingsResult(PublicProfileSettingsStatus Status, string? Error = null);
