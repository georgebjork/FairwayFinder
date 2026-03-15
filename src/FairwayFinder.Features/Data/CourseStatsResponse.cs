namespace FairwayFinder.Features.Data;

/// <summary>
/// Complete course-specific stats for a user at a particular course.
/// </summary>
public class CourseStatsResponse
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of rounds played at this course (excluding ExcludeFromStats rounds).
    /// </summary>
    public int TotalRounds { get; set; }
    
    /// <summary>
    /// Average 18-hole score at this course. Null if no 18-hole rounds.
    /// </summary>
    public double? Average18HoleScore { get; set; }
    
    /// <summary>
    /// Average 9-hole score at this course. Null if no 9-hole rounds.
    /// </summary>
    public double? Average9HoleScore { get; set; }
    
    /// <summary>
    /// Best 18-hole score at this course. Null if no 18-hole rounds.
    /// </summary>
    public BestRound? Best18HoleRound { get; set; }
    
    /// <summary>
    /// Best 9-hole score at this course. Null if no 9-hole rounds.
    /// </summary>
    public BestRound? Best9HoleRound { get; set; }
    
    /// <summary>
    /// Per-hole aggregate stats across all rounds at this course.
    /// </summary>
    public List<HoleAggregateStats> HoleStats { get; set; } = new();
    
    /// <summary>
    /// Aggregate scoring distribution across all rounds at this course.
    /// </summary>
    public ScoringDistribution ScoringDistribution { get; set; } = new();
    
    /// <summary>
    /// All rounds played at this course (for the round history table).
    /// </summary>
    public List<RoundResponse> Rounds { get; set; } = new();
    
    /// <summary>
    /// Available teeboxes the user has played at this course (for filter dropdown).
    /// </summary>
    public List<CourseTeeboxOption> TeeboxOptions { get; set; } = new();
    
    /// <summary>
    /// The teebox ID currently being filtered on. Null = all teeboxes.
    /// </summary>
    public long? SelectedTeeboxId { get; set; }
}

/// <summary>
/// Lightweight DTO for populating a teebox filter dropdown on course stats.
/// </summary>
public class CourseTeeboxOption
{
    public long TeeboxId { get; set; }
    public string TeeboxName { get; set; } = string.Empty;
    public int RoundCount { get; set; }
}

/// <summary>
/// Aggregated stats for a single hole number across all rounds at a course.
/// </summary>
public class HoleAggregateStats
{
    public int HoleNumber { get; set; }
    
    /// <summary>
    /// Most common par for this hole (across different teeboxes).
    /// </summary>
    public int Par { get; set; }
    
    /// <summary>
    /// Most common handicap for this hole (across different teeboxes).
    /// </summary>
    public int Handicap { get; set; }
    
    /// <summary>
    /// Average yardage across teeboxes played.
    /// </summary>
    public int AverageYardage { get; set; }
    
    /// <summary>
    /// Number of times this hole was played.
    /// </summary>
    public int TimesPlayed { get; set; }
    
    /// <summary>
    /// Average score on this hole.
    /// </summary>
    public decimal AverageScore { get; set; }
    
    /// <summary>
    /// Average score relative to par (negative = under par).
    /// </summary>
    public decimal AverageScoreToPar { get; set; }
    
    /// <summary>
    /// Fairway hit percentage. Null if no fairway data or par 3 hole.
    /// </summary>
    public decimal? FairwayHitPercent { get; set; }
    
    /// <summary>
    /// Green in regulation percentage. Null if no GIR data.
    /// </summary>
    public decimal? GirPercent { get; set; }
    
    /// <summary>
    /// Average number of putts. Null if no putting data.
    /// </summary>
    public decimal? AveragePutts { get; set; }
    
    /// <summary>
    /// Fairway miss direction breakdown. Null if no fairway miss data or par 3.
    /// </summary>
    public MissBreakdown? FairwayMiss { get; set; }
    
    /// <summary>
    /// Green miss direction breakdown. Null if no green miss data.
    /// </summary>
    public MissBreakdown? GreenMiss { get; set; }
}

/// <summary>
/// Breakdown of miss directions (left, right, short, long) with the primary miss identified.
/// </summary>
public class MissBreakdown
{
    public int LeftCount { get; set; }
    public int RightCount { get; set; }
    public int ShortCount { get; set; }
    public int LongCount { get; set; }
    public int OtherCount { get; set; }
    public int TotalMisses { get; set; }
    
    /// <summary>
    /// The most common miss direction with percentage, e.g. "Left (60%)".
    /// Null if no misses recorded.
    /// </summary>
    public string? PrimaryMiss
    {
        get
        {
            if (TotalMisses == 0) return null;
            
            var directions = new (string Name, int Count)[]
            {
                ("Left", LeftCount),
                ("Right", RightCount),
                ("Short", ShortCount),
                ("Long", LongCount),
                ("Other", OtherCount)
            };
            
            var top = directions.OrderByDescending(d => d.Count).First();
            if (top.Count == 0) return null;
            
            var percent = Math.Round((decimal)top.Count / TotalMisses * 100, 0);
            return $"{top.Name} ({percent}%)";
        }
    }

}
