using FairwayFinder.Data;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class RoundService : IRoundService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public RoundService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<RoundResponse>> GetRoundsByUserIdAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var rounds = await dbContext.Rounds
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .Join(
                dbContext.Courses,
                round => round.CourseId,
                course => course.CourseId,
                (round, course) => new RoundResponse
                {
                    RoundId = round.RoundId,
                    DatePlayed = round.DatePlayed,
                    CourseName = course.CourseName,
                    Score = round.Score
                })
            .OrderByDescending(r => r.DatePlayed)
            .ToListAsync();

        return rounds;
    }

    public async Task<ScorecardDto?> GetRoundScorecardAsync(long roundId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        // Get round with course and teebox info
        var roundData = await (
            from round in dbContext.Rounds
            join course in dbContext.Courses on round.CourseId equals course.CourseId
            join teebox in dbContext.Teeboxes on round.TeeboxId equals teebox.TeeboxId
            where round.RoundId == roundId && !round.IsDeleted
            select new
            {
                Round = round,
                Course = course,
                Teebox = teebox
            }).FirstOrDefaultAsync();

        if (roundData is null)
        {
            return null;
        }

        // Get holes for this teebox
        var holes = await dbContext.Holes
            .Where(h => h.TeeboxId == roundData.Teebox.TeeboxId && !h.IsDeleted)
            .OrderBy(h => h.HoleNumber)
            .ToListAsync();

        // Get scores for this round
        var scores = await dbContext.Scores
            .Where(s => s.RoundId == roundId && !s.IsDeleted)
            .ToListAsync();

        // Get hole stats for this round (if using advanced stats)
        var holeStats = await dbContext.HoleStats
            .Where(hs => hs.RoundId == roundId && !hs.IsDeleted)
            .ToListAsync();

        // Build scorecard DTO
        var scorecard = new ScorecardDto
        {
            RoundId = roundData.Round.RoundId,
            DatePlayed = roundData.Round.DatePlayed,
            CourseName = roundData.Course.CourseName,
            TeeboxName = roundData.Teebox.TeeboxName,
            TeeboxPar = roundData.Teebox.Par,
            Rating = roundData.Teebox.Rating,
            Slope = roundData.Teebox.Slope,
            YardageOut = roundData.Teebox.YardageOut,
            YardageIn = roundData.Teebox.YardageIn,
            YardageTotal = roundData.Teebox.YardageTotal,
            Score = roundData.Round.Score,
            ScoreOut = roundData.Round.ScoreOut,
            ScoreIn = roundData.Round.ScoreIn,
            UsingHoleStats = roundData.Round.UsingHoleStats,
            Holes = holes.Select(h =>
            {
                var score = scores.FirstOrDefault(s => s.HoleId == h.HoleId);
                var holeStat = holeStats.FirstOrDefault(hs => hs.HoleId == h.HoleId);
                return new ScorecardHoleDto
                {
                    HoleId = h.HoleId,
                    HoleNumber = h.HoleNumber,
                    Par = h.Par,
                    Yardage = h.Yardage,
                    Handicap = h.Handicap,
                    HoleScore = score?.HoleScore,
                    // Stats
                    HitFairway = holeStat?.HitFairway,
                    MissFairwayType = holeStat?.MissFairwayType,
                    HitGreen = holeStat?.HitGreen,
                    MissGreenType = holeStat?.MissGreenType,
                    NumberOfPutts = holeStat?.NumberOfPutts
                };
            }).ToList()
        };

        return scorecard;
    }
}
