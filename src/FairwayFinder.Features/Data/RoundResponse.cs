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
    public bool UsingShotTracking { get; set; }
    public bool ExcludeFromStats { get; set; }
    public bool FullRound { get; set; }
    
    // Course info
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    
    // Teebox info
    public RoundTeebox Teebox { get; set; } = new();
    
    // Aggregated scoring (from RoundStat table)
    public RoundStats? Stats { get; set; }
    
    // Individual hole data
    public List<RoundHole> Holes { get; set; } = new();

    // Shot-by-shot data and SG
    public StrokesGainedSummary? StrokesGained { get; set; }
    public List<StrokesGainedHoleResult>? HoleByHoleSg { get; set; }

    // Computed properties - Par
    public int ParOut => Holes.Where(h => h.HoleNumber <= 9).Sum(h => h.Par);
    public int ParIn => Holes.Where(h => h.HoleNumber > 9).Sum(h => h.Par);
    public int ScoreToPar
    {
        get
        {
            if (FullRound || Teebox.IsNineHole) return Score - Teebox.Par;
            // 9-hole round on an 18-hole teebox: use exact front/back par if holes are loaded,
            // otherwise fall back to half of teebox par (correct for standard 36/36 courses).
            if (Holes.Count > 0)
            {
                var hasFront = Holes.Any(h => h.HoleNumber <= 9);
                var hasBack = Holes.Any(h => h.HoleNumber > 9);
                if (hasFront && !hasBack) return Score - ParOut;
                if (hasBack && !hasFront) return Score - ParIn;
            }
            return Score - Teebox.Par / 2;
        }
    }

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
            UsingShotTracking = round.UsingShotTracking,
            ExcludeFromStats = round.ExcludeFromStats,
            CourseId = course.CourseId,
            CourseName = course.CourseName,
            Teebox = RoundTeebox.From(teebox),
            Stats = roundStat != null ? RoundStats.From(roundStat) : null,
            Holes = holes ?? new(),
            FullRound = round.FullRound,
            StrokesGained = roundStat?.SgTotal.HasValue == true
                ? new StrokesGainedSummary
                {
                    SgTotal = roundStat.SgTotal.Value,
                    SgPutting = roundStat.SgPutting ?? 0,
                    SgTeeToGreen = roundStat.SgTeeToGreen ?? 0,
                    SgOffTheTee = roundStat.SgOffTheTee ?? 0,
                    SgApproach = roundStat.SgApproach ?? 0,
                    SgAroundTheGreen = roundStat.SgAroundTheGreen ?? 0,
                    RoundsIncluded = 1,
                    HolesWithShots = holes?.Count(h => h.Shots is { Count: > 0 }) ?? 0
                }
                : null
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
    public int Par { get; set; }
    public decimal Rating { get; set; }
    public int Slope { get; set; }
    public int YardageOut { get; set; }
    public int YardageIn { get; set; }
    public int YardageTotal { get; set; }
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
    public long ScoreId { get; set; }
    public long HoleId { get; set; }
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int Yardage { get; set; }
    public int Handicap { get; set; }
    
    // Score (null if not played)
    public short? Score { get; set; }
    
    // Advanced stats (null if not tracking)
    public RoundHoleStat? Stats { get; set; }

    // Shot data (null if not tracking shots)
    public List<ShotData>? Shots { get; set; }

    // Computed property for score relative to par
    public int? ScoreToPar => Score.HasValue ? Score.Value - Par : null;

    public static RoundHole From(Hole hole, Score? score, HoleStat? holeStat)
    {
        return new RoundHole
        {
            ScoreId = score?.ScoreId ?? 0,
            HoleId = hole.HoleId,
            HoleNumber = hole.HoleNumber,
            Par = hole.Par,
            Yardage = hole.Yardage,
            Handicap = hole.Handicap,
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
    public long? MissFairwayType { get; set; }
    public bool? HitGreen { get; set; }
    public long? MissGreenType { get; set; }
    public short? NumberOfPutts { get; set; }
    public int? ApproachYardage { get; set; }

    public static RoundHoleStat From(HoleStat holeStat)
    {
        return new RoundHoleStat
        {
            HitFairway = holeStat.HitFairway,
            MissFairwayType = holeStat.MissFairwayType,
            HitGreen = holeStat.HitGreen,
            MissGreenType = holeStat.MissGreenType,
            NumberOfPutts = holeStat.NumberOfPutts,
            ApproachYardage = holeStat.ApproachYardage
        };
    }
}
