namespace FairwayFinder.Features.Data;

/// <summary>
/// Quick stats for the dashboard header
/// </summary>
public class DashboardStatsDto
{
    public int TotalRounds { get; set; }
    public double? AverageScore { get; set; }
    public BestRoundDto? BestRound { get; set; }
}

/// <summary>
/// Best round information
/// </summary>
public class BestRoundDto
{
    public long RoundId { get; set; }
    public int Score { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public DateOnly DatePlayed { get; set; }
}

/// <summary>
/// A single data point for the score trend chart
/// </summary>
public class ScoreTrendPointDto
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public int Score { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// Advanced stats (FIR, GIR, Putting) aggregated across rounds with hole-by-hole tracking
/// </summary>
public class AdvancedStatsDto
{
    /// <summary>
    /// Fairways In Regulation percentage (par 4/5 holes only)
    /// </summary>
    public double? FirPercent { get; set; }
    
    /// <summary>
    /// Greens In Regulation percentage (all holes)
    /// </summary>
    public double? GirPercent { get; set; }
    
    /// <summary>
    /// Average putts per round
    /// </summary>
    public double? AveragePutts { get; set; }
    
    /// <summary>
    /// Number of rounds that have advanced stats tracked
    /// </summary>
    public int RoundsWithStats { get; set; }
}

/// <summary>
/// Scoring distribution aggregated across all rounds (from RoundStat table)
/// </summary>
public class ScoringDistributionDto
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
/// Course with round count and average score for "Most Played Courses" list
/// </summary>
public class MostPlayedCourseDto
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int RoundCount { get; set; }
    public double AverageScore { get; set; }
}

/// <summary>
/// Average scoring by par type (Par 3, Par 4, Par 5)
/// </summary>
public class ParTypeScoringDto
{
    public double? Par3Average { get; set; }
    public double? Par4Average { get; set; }
    public double? Par5Average { get; set; }
    public int Par3Count { get; set; }
    public int Par4Count { get; set; }
    public int Par5Count { get; set; }
}
