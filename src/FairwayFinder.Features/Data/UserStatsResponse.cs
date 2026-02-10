namespace FairwayFinder.Features.Data;

/// <summary>
/// Complete user stats aggregated across all rounds.
/// </summary>
public class UserStatsResponse
{
    // Basic stats
    public int TotalRounds { get; set; }
    public int Total18HoleRounds { get; set; }
    public int Total9HoleRounds { get; set; }
    public double? Average18HoleScore { get; set; }
    public double? Average9HoleScore { get; set; }
    public double? Average18HoleScoreTrend { get; set; }
    public double? Average9HoleScoreTrend { get; set; }
    public BestRound? Best18HoleRound { get; set; }
    public BestRound? Best9HoleRound { get; set; }
    
    // Advanced stats (FIR, GIR, putts)
    public AdvancedStats AdvancedStats { get; set; } = new();
    
    // Par type averages
    public ParTypeScoring ParTypeScoring { get; set; } = new();
    
    // Trend/chart data
    public List<ScoreTrendPoint> ScoreTrend18Hole { get; set; } = new();
    public List<ScoreTrendPoint> ScoreTrend9Hole { get; set; } = new();
    public List<PuttsTrendPoint> PuttsTrend18Hole { get; set; } = new();
    public List<PuttsTrendPoint> PuttsTrend9Hole { get; set; } = new();
    public List<FirTrendPoint> FirTrend { get; set; } = new();
    public List<GirTrendPoint> GirTrend { get; set; } = new();
    public ScoringDistribution ScoringDistribution { get; set; } = new();
    public List<CourseStats> MostPlayedCourses { get; set; } = new();
    
    /// <summary>
    /// The filtered rounds used to calculate these stats.
    /// Useful for displaying a rounds list alongside stats with matching filters.
    /// </summary>
    public List<RoundResponse> Rounds { get; set; } = new();
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
/// Advanced stats (FIR, GIR, Putting) aggregated across rounds
/// </summary>
public class AdvancedStats
{
    /// <summary>
    /// Fairways In Regulation percentage (par 4/5 holes only)
    /// </summary>
    public double? FirPercent { get; set; }
    
    /// <summary>
    /// FIR% trend (last 5 rounds vs previous 5 rounds). Positive = improvement.
    /// </summary>
    public double? FirPercentTrend { get; set; }
    
    /// <summary>
    /// Greens In Regulation percentage (all holes)
    /// </summary>
    public double? GirPercent { get; set; }
    
    /// <summary>
    /// GIR% trend (last 5 rounds vs previous 5 rounds). Positive = improvement.
    /// </summary>
    public double? GirPercentTrend { get; set; }
    
    /// <summary>
    /// Average putts per round
    /// </summary>
    public double? Average18HolePutts { get; set; }
    public double? Average9HolePutts { get; set; }
    
    /// <summary>
    /// Average putts trend (last 5 rounds vs previous 5 rounds). Negative = improvement.
    /// </summary>
    public double? Average18HolePuttsTrend { get; set; }
    public double? Average9HolePuttsTrend { get; set; }
    
    /// <summary>
    /// Number of rounds that have advanced stats tracked
    /// </summary>
    public int RoundsWithStats { get; set; }
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
    public int RoundCount { get; set; }
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
    public double? Par3Average { get; set; }
    public double? Par4Average { get; set; }
    public double? Par5Average { get; set; }
    public int Par3Count { get; set; }
    public int Par4Count { get; set; }
    public int Par5Count { get; set; }
}
