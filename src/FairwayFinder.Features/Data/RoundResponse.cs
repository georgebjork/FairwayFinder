using FairwayFinder.Data.Entities;

namespace FairwayFinder.Features.Data;

/// <summary>
/// Complete round data with all related information
/// </summary>
public class RoundResponse
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public int Score { get; set; }
    public int ScoreOut { get; set; }
    public int ScoreIn { get; set; }
    public bool UsingHoleStats { get; set; }
    public bool ExcludeFromStats { get; set; }
    
    // Course info
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    
    // Teebox info
    public RoundTeebox Teebox { get; set; } = new();
    
    // Aggregated scoring (from RoundStat table)
    public RoundStats? Stats { get; set; }
    
    // Individual hole data
    public List<RoundHole> Holes { get; set; } = new();

    // Computed properties - Par
    public int ParOut => Holes.Where(h => h.HoleNumber <= 9).Sum(h => h.Par);
    public int ParIn => Holes.Where(h => h.HoleNumber > 9).Sum(h => h.Par);
    public int ScoreToPar => Score - (int)Teebox.Par;

    // Computed properties - Fairways (only par 4/5 holes have fairways)
    public int FairwaysHitOut => Holes.Count(h => h.HoleNumber <= 9 && h.Stats?.HitFairway == true);
    public int FairwaysHitIn => Holes.Count(h => h.HoleNumber > 9 && h.Stats?.HitFairway == true);
    public int FairwaysHit => FairwaysHitOut + FairwaysHitIn;
    public int FairwaysTotal => Holes.Count(h => h.Par > 3);

    // Computed properties - Greens in Regulation
    public int GreensHitOut => Holes.Count(h => h.HoleNumber <= 9 && h.Stats?.HitGreen == true);
    public int GreensHitIn => Holes.Count(h => h.HoleNumber > 9 && h.Stats?.HitGreen == true);
    public int GreensHit => GreensHitOut + GreensHitIn;
    public int GreensTotal => Holes.Count;

    // Computed properties - Putts
    public int PuttsOut => Holes.Where(h => h.HoleNumber <= 9 && h.Stats?.NumberOfPutts.HasValue == true).Sum(h => h.Stats!.NumberOfPutts!.Value);
    public int PuttsIn => Holes.Where(h => h.HoleNumber > 9 && h.Stats?.NumberOfPutts.HasValue == true).Sum(h => h.Stats!.NumberOfPutts!.Value);
    public int TotalPutts => PuttsOut + PuttsIn;

    public static RoundResponse From(
        Round round,
        Course course,
        Teebox teebox,
        RoundStat? roundStat = null,
        List<RoundHole>? holes = null)
    {
        return new RoundResponse
        {
            RoundId = round.RoundId,
            DatePlayed = round.DatePlayed,
            Score = round.Score,
            ScoreOut = round.ScoreOut,
            ScoreIn = round.ScoreIn,
            UsingHoleStats = round.UsingHoleStats,
            ExcludeFromStats = round.ExcludeFromStats,
            CourseId = course.CourseId,
            CourseName = course.CourseName,
            Teebox = RoundTeebox.From(teebox),
            Stats = roundStat != null ? RoundStats.From(roundStat) : null,
            Holes = holes ?? new()
        };
    }
}

/// <summary>
/// Teebox information for a round
/// </summary>
public class RoundTeebox
{
    public long TeeboxId { get; set; }
    public string TeeboxName { get; set; } = string.Empty;
    public long Par { get; set; }
    public decimal Rating { get; set; }
    public long Slope { get; set; }
    public long YardageOut { get; set; }
    public long YardageIn { get; set; }
    public long YardageTotal { get; set; }
    public bool IsNineHole { get; set; }

    public static RoundTeebox From(Teebox teebox)
    {
        return new RoundTeebox
        {
            TeeboxId = teebox.TeeboxId,
            TeeboxName = teebox.TeeboxName,
            Par = teebox.Par,
            Rating = teebox.Rating,
            Slope = teebox.Slope,
            YardageOut = teebox.YardageOut,
            YardageIn = teebox.YardageIn,
            YardageTotal = teebox.YardageTotal,
            IsNineHole = teebox.IsNineHole
        };
    }
}

/// <summary>
/// Individual hole data including score and stats
/// </summary>
public class RoundHole
{
    public long HoleId { get; set; }
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int Yardage { get; set; }
    public int Handicap { get; set; }
    
    // Score (null if not played)
    public short? Score { get; set; }
    
    // Advanced stats (null if not tracking)
    public RoundHoleStat? Stats { get; set; }

    // Computed property for score relative to par
    public int? ScoreToPar => Score.HasValue ? Score.Value - Par : null;

    public static RoundHole From(Hole hole, Score? score, HoleStat? holeStat)
    {
        return new RoundHole
        {
            HoleId = hole.HoleId,
            HoleNumber = (int)hole.HoleNumber,
            Par = (int)hole.Par,
            Yardage = (int)hole.Yardage,
            Handicap = (int)hole.Handicap,
            Score = score?.HoleScore,
            Stats = holeStat != null ? RoundHoleStat.From(holeStat) : null
        };
    }
}

/// <summary>
/// Aggregated round stats (from RoundStat table)
/// </summary>
public class RoundStats
{
    public int HolesInOne { get; set; }
    public int DoubleEagles { get; set; }
    public int Eagles { get; set; }
    public int Birdies { get; set; }
    public int Pars { get; set; }
    public int Bogeys { get; set; }
    public int DoubleBogeys { get; set; }
    public int TripleOrWorse { get; set; }

    public static RoundStats From(RoundStat roundStat)
    {
        return new RoundStats
        {
            HolesInOne = roundStat.HoleInOne,
            DoubleEagles = roundStat.DoubleEagles,
            Eagles = roundStat.Eagles,
            Birdies = roundStat.Birdies,
            Pars = roundStat.Pars,
            Bogeys = roundStat.Bogies,
            DoubleBogeys = roundStat.DoubleBogies,
            TripleOrWorse = roundStat.TripleOrWorse
        };
    }
}

/// <summary>
/// Advanced hole stats (FIR, GIR, putts)
/// </summary>
public class RoundHoleStat
{
    public bool? HitFairway { get; set; }
    public int? MissFairwayType { get; set; }
    public bool? HitGreen { get; set; }
    public int? MissGreenType { get; set; }
    public short? NumberOfPutts { get; set; }

    public static RoundHoleStat From(HoleStat holeStat)
    {
        return new RoundHoleStat
        {
            HitFairway = holeStat.HitFairway,
            MissFairwayType = holeStat.MissFairwayType,
            HitGreen = holeStat.HitGreen,
            MissGreenType = holeStat.MissGreenType,
            NumberOfPutts = holeStat.NumberOfPutts
        };
    }
}
