namespace FairwayFinder.Features.Data;

/// <summary>
/// Complete user stats aggregated across all rounds, grouped by area of the game.
/// Each group carries its own <c>RoundsIncluded</c> count so consumers can tell
/// how many rounds backed the numbers without inspecting the raw data.
/// </summary>
public class UserStatsResponse
{
    /// <summary>Total rounds included after filtering.</summary>
    public int TotalRounds { get; set; }

    /// <summary>Scoring — averages, trends, and best rounds. Backed by every round.</summary>
    public ScoringStats Scoring { get; set; } = new();

    /// <summary>Ball striking — FIR and GIR. Backed by rounds with hole-by-hole tracking.</summary>
    public BallStrikingStats BallStriking { get; set; } = new();

    /// <summary>Short game — putting, 3-putts, and up-and-down. Backed by rounds with hole-by-hole tracking.</summary>
    public ShortGameStats ShortGame { get; set; } = new();

    /// <summary>Average scoring by par type (Par 3, 4, 5).</summary>
    public ParTypeScoring ParTypeScoring { get; set; } = new();

    /// <summary>Hole-by-hole scoring distribution (birdies, pars, bogeys, ...).</summary>
    public ScoringDistribution ScoringDistribution { get; set; } = new();

    /// <summary>Strokes gained. Backed by rounds with shot-by-shot tracking.</summary>
    public StrokesGainedStats StrokesGained { get; set; } = new();

    /// <summary>The user's most-played courses.</summary>
    public List<CourseStats> MostPlayedCourses { get; set; } = new();
}

/// <summary>
/// Scoring stats — averages, trends, and best rounds. Backed by every round
/// (a total score is always available, regardless of hole-stat tracking).
/// </summary>
public class ScoringStats
{
    /// <summary>Number of rounds backing these stats (all included rounds).</summary>
    public int RoundsIncluded { get; set; }

    /// <summary>Number of included 18-hole rounds.</summary>
    public int Rounds18Hole { get; set; }

    /// <summary>Number of included 9-hole rounds.</summary>
    public int Rounds9Hole { get; set; }

    public double? Average18HoleScore { get; set; }
    public double? Average9HoleScore { get; set; }

    /// <summary>Score trend (linear regression slope). Negative = improvement.</summary>
    public double? Average18HoleScoreTrend { get; set; }
    public double? Average9HoleScoreTrend { get; set; }

    public BestRound? Best18HoleRound { get; set; }
    public BestRound? Best9HoleRound { get; set; }

    /// <summary>Per-round score data for charting (oldest to newest).</summary>
    public List<ScoreTrendPoint> ScoreTrend18Hole { get; set; } = new();
    public List<ScoreTrendPoint> ScoreTrend9Hole { get; set; } = new();
}

/// <summary>
/// Ball-striking stats (FIR, GIR). Backed by rounds that opted into
/// hole-by-hole stat tracking.
/// </summary>
public class BallStrikingStats
{
    /// <summary>Number of hole-tracked rounds backing these stats.</summary>
    public int RoundsIncluded { get; set; }

    /// <summary>Fairways In Regulation percentage (par 4/5 holes only).</summary>
    public double? FirPercent { get; set; }

    /// <summary>FIR% trend (linear regression slope). Positive = improvement.</summary>
    public double? FirPercentTrend { get; set; }

    /// <summary>Greens In Regulation percentage (all holes).</summary>
    public double? GirPercent { get; set; }

    /// <summary>GIR% trend (linear regression slope). Positive = improvement.</summary>
    public double? GirPercentTrend { get; set; }

    /// <summary>Off-the-tee penalty percentage (all holes; par 3 uses the approach flag).</summary>
    public double? TeePenaltyPercent { get; set; }

    /// <summary>Tee-penalty% trend (linear regression slope). Negative = improvement (fewer penalties).</summary>
    public double? TeePenaltyPercentTrend { get; set; }

    /// <summary>Per-round FIR% data for charting (oldest to newest).</summary>
    public List<FirTrendPoint> FirTrend { get; set; } = new();

    /// <summary>Per-round GIR% data for charting (oldest to newest).</summary>
    public List<GirTrendPoint> GirTrend { get; set; } = new();

    /// <summary>Per-round off-the-tee penalty% data for charting (oldest to newest).</summary>
    public List<TeePenaltyTrendPoint> TeePenaltyTrend { get; set; } = new();
}

/// <summary>
/// Short-game stats (putting, 3-putts, up-and-down). Backed by rounds that
/// opted into hole-by-hole stat tracking.
/// </summary>
public class ShortGameStats
{
    /// <summary>Number of hole-tracked rounds backing these stats.</summary>
    public int RoundsIncluded { get; set; }

    /// <summary>Up and Down percentage (all holes).</summary>
    public double? UpAndDownPercent { get; set; }

    /// <summary>Up and Down% trend (linear regression slope). Positive = improvement.</summary>
    public double? UpAndDownPercentTrend { get; set; }

    /// <summary>Average putts per round.</summary>
    public double? Average18HolePutts { get; set; }
    public double? Average9HolePutts { get; set; }

    /// <summary>Average putts trend (linear regression slope). Negative = improvement.</summary>
    public double? Average18HolePuttsTrend { get; set; }
    public double? Average9HolePuttsTrend { get; set; }

    /// <summary>Average number of 3-putts (or worse) per round.</summary>
    public double? Average18HoleThreePutts { get; set; }
    public double? Average9HoleThreePutts { get; set; }

    /// <summary>Average 3-putts trend (linear regression slope). Negative = improvement.</summary>
    public double? Average18HoleThreePuttsTrend { get; set; }
    public double? Average9HoleThreePuttsTrend { get; set; }

    /// <summary>Per-round putts data for charting (oldest to newest).</summary>
    public List<PuttsTrendPoint> PuttsTrend18Hole { get; set; } = new();
    public List<PuttsTrendPoint> PuttsTrend9Hole { get; set; } = new();

    /// <summary>Per-round 3-putt counts for charting (oldest to newest).</summary>
    public List<ThreePuttsTrendPoint> ThreePuttsTrend18Hole { get; set; } = new();
    public List<ThreePuttsTrendPoint> ThreePuttsTrend9Hole { get; set; } = new();

    /// <summary>Per-round up-and-down% data for charting (oldest to newest).</summary>
    public List<UpAndDownTrendPoint> UpAndDownTrend { get; set; } = new();
}

/// <summary>
/// Strokes gained stats. Backed by rounds with shot-by-shot tracking.
/// </summary>
public class StrokesGainedStats
{
    /// <summary>Number of shot-tracked rounds backing these stats.</summary>
    public int RoundsIncluded { get; set; }

    /// <summary>Aggregate SG summary. Null when no shot-tracked rounds are included.</summary>
    public StrokesGainedSummary? Summary { get; set; }

    /// <summary>Per-round SG:Total data for charting (oldest to newest).</summary>
    public List<StrokesGainedTrendPoint> TotalTrend { get; set; } = new();

    /// <summary>Per-round SG:Putting data for charting (oldest to newest).</summary>
    public List<StrokesGainedTrendPoint> PuttingTrend { get; set; } = new();

    /// <summary>Per-round SG:Tee-to-Green data for charting (oldest to newest).</summary>
    public List<StrokesGainedTrendPoint> TeeToGreenTrend { get; set; } = new();
}

/// <summary>
/// Best round information
/// </summary>
public class BestRound
{
    public long RoundId { get; set; }
    public int Score { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public DateOnly DatePlayed { get; set; }
}

/// <summary>
/// A single data point for score trend
/// </summary>
public class ScoreTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public int Score { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// A single data point for a trend line (linear regression).
/// Reusable across score, putts, FIR%, GIR%, etc.
/// </summary>
public class TrendLinePoint
{
    public string DateLabel { get; set; } = string.Empty;
    public double Value { get; set; }
}

/// <summary>
/// A single data point for putts trend
/// </summary>
public class PuttsTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public int Putts { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// A single data point for FIR% trend
/// </summary>
public class FirTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public double FirPercent { get; set; }
    public int FairwaysHit { get; set; }
    public int FairwayAttempts { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// A single data point for GIR% trend
/// </summary>
public class GirTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public double GirPercent { get; set; }
    public int GreensHit { get; set; }
    public int GreenAttempts { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// A single data point for off-the-tee penalty% trend
/// </summary>
public class TeePenaltyTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public double TeePenaltyPercent { get; set; }
    public int PenaltyHoles { get; set; }
    public int TeeShotAttempts { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// A single data point for up-and-down% trend
/// </summary>
public class UpAndDownTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public double UpAndDownPercent { get; set; }
    public int UpAndDowns { get; set; }
    public int Attempts { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// A single data point for 3-putts trend (count of 3-putt-or-worse holes in a round)
/// </summary>
public class ThreePuttsTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public int ThreePutts { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// Scoring distribution aggregated across all rounds
/// </summary>
public class ScoringDistribution
{
    public int HolesInOne { get; set; }
    public int DoubleEagles { get; set; }
    public int Eagles { get; set; }
    public int Birdies { get; set; }
    public int Pars { get; set; }
    public int Bogeys { get; set; }
    public int DoubleBogeys { get; set; }
    public int TripleOrWorse { get; set; }

    /// <summary>
    /// Total holes played (sum of all categories)
    /// </summary>
    public int TotalHoles => HolesInOne + DoubleEagles + Eagles + Birdies + Pars + Bogeys + DoubleBogeys + TripleOrWorse;

    /// <summary>
    /// Number of rounds included in this distribution
    /// </summary>
    public int RoundsIncluded { get; set; }
}

/// <summary>
/// Stats for a specific course
/// </summary>
public class CourseStats
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int RoundCount { get; set; }
    public double? Average18HoleScore { get; set; }
    public double? Average9HoleScore { get; set; }
}

/// <summary>
/// Average scoring by par type (Par 3, Par 4, Par 5)
/// </summary>
public class ParTypeScoring
{
    /// <summary>Number of rounds that contributed scored holes.</summary>
    public int RoundsIncluded { get; set; }

    public double? Par3Average { get; set; }
    public double? Par4Average { get; set; }
    public double? Par5Average { get; set; }
    public int Par3Count { get; set; }
    public int Par4Count { get; set; }
    public int Par5Count { get; set; }
}
