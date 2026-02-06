namespace FairwayFinder.Features.Data;

/// <summary>
/// Complete scorecard data for displaying a round
/// </summary>
public class ScorecardDto
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    
    // Course info
    public string CourseName { get; set; } = string.Empty;
    
    // Teebox info
    public string TeeboxName { get; set; } = string.Empty;
    public long TeeboxPar { get; set; }
    public decimal Rating { get; set; }
    public long Slope { get; set; }
    public long YardageOut { get; set; }
    public long YardageIn { get; set; }
    public long YardageTotal { get; set; }
    
    // Round scores
    public int Score { get; set; }
    public int ScoreOut { get; set; }
    public int ScoreIn { get; set; }
    
    // Whether this round has advanced hole stats
    public bool UsingHoleStats { get; set; }
    
    // Hole details (should be 18 items for full round, 9 for partial)
    public List<ScorecardHoleDto> Holes { get; set; } = new();
    
    // Computed properties - Par
    public int ParOut => Holes.Where(h => h.HoleNumber <= 9).Sum(h => (int)h.Par);
    public int ParIn => Holes.Where(h => h.HoleNumber > 9).Sum(h => (int)h.Par);
    public int ScoreToPar => Score - (int)TeeboxPar;
    
    // Computed properties - Fairways (only count non-par-3 holes)
    public int FairwaysHitOut => Holes.Count(h => h.HoleNumber <= 9 && h.HitFairway == true);
    public int FairwaysHitIn => Holes.Count(h => h.HoleNumber > 9 && h.HitFairway == true);
    public int FairwaysHit => FairwaysHitOut + FairwaysHitIn;
    public int FairwaysTotal => Holes.Count(h => h.Par > 3);
    
    // Computed properties - Greens in Regulation
    public int GreensHitOut => Holes.Count(h => h.HoleNumber <= 9 && h.HitGreen == true);
    public int GreensHitIn => Holes.Count(h => h.HoleNumber > 9 && h.HitGreen == true);
    public int GreensHit => GreensHitOut + GreensHitIn;
    public int GreensTotal => Holes.Count;
    
    // Computed properties - Putts
    public int PuttsOut => Holes.Where(h => h.HoleNumber <= 9 && h.NumberOfPutts.HasValue).Sum(h => h.NumberOfPutts!.Value);
    public int PuttsIn => Holes.Where(h => h.HoleNumber > 9 && h.NumberOfPutts.HasValue).Sum(h => h.NumberOfPutts!.Value);
    public int TotalPutts => PuttsOut + PuttsIn;
}

/// <summary>
/// Individual hole data for the scorecard
/// </summary>
public class ScorecardHoleDto
{
    public long HoleId { get; set; }
    public long HoleNumber { get; set; }
    public long Par { get; set; }
    public long Yardage { get; set; }
    public long Handicap { get; set; }
    
    // Score for this hole (null if not played)
    public short? HoleScore { get; set; }
    
    // Computed property for score relative to par
    public int? ScoreToPar => HoleScore.HasValue ? HoleScore.Value - (int)Par : null;
    
    // Stats (from HoleStat table)
    public bool? HitFairway { get; set; }
    public int? MissFairwayType { get; set; }  // 1=Left, 2=Right, 3=Short, 4=Long
    public bool? HitGreen { get; set; }
    public int? MissGreenType { get; set; }    // 1=Left, 2=Right, 3=Short, 4=Long
    public short? NumberOfPutts { get; set; }
}
