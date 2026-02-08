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

        var roundsData = await dbContext.Rounds
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .Join(dbContext.Courses, r => r.CourseId, c => c.CourseId, (r, c) => new { Round = r, Course = c })
            .Join(dbContext.Teeboxes, rc => rc.Round.TeeboxId, t => t.TeeboxId, (rc, t) => new { rc.Round, rc.Course, Teebox = t })
            .OrderByDescending(x => x.Round.DatePlayed)
            .ToListAsync();

        return roundsData
            .Select(x => RoundResponse.From(x.Round, x.Course, x.Teebox))
            .ToList();
    }

    public async Task<List<RoundResponse>> GetRoundsWithDetailsAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // 1. Get rounds with course and teebox
        var roundsData = await dbContext.Rounds
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .Join(dbContext.Courses, r => r.CourseId, c => c.CourseId, (r, c) => new { Round = r, Course = c })
            .Join(dbContext.Teeboxes, rc => rc.Round.TeeboxId, t => t.TeeboxId, (rc, t) => new { rc.Round, rc.Course, Teebox = t })
            .OrderByDescending(x => x.Round.DatePlayed)
            .ToListAsync();

        if (roundsData.Count == 0)
        {
            return new List<RoundResponse>();
        }

        var roundIds = roundsData.Select(r => r.Round.RoundId).ToList();

        // 2. Get round stats
        var roundStats = await dbContext.RoundStats
            .Where(rs => roundIds.Contains(rs.RoundId) && !rs.IsDeleted)
            .ToDictionaryAsync(rs => rs.RoundId);

        // 3. Get scores
        var scores = await dbContext.Scores
            .Where(s => roundIds.Contains(s.RoundId) && !s.IsDeleted)
            .ToListAsync();

        // 4. Get holes for all scores
        var holeIds = scores.Select(s => s.HoleId).Distinct().ToList();
        var holes = await dbContext.Holes
            .Where(h => holeIds.Contains(h.HoleId) && !h.IsDeleted)
            .ToDictionaryAsync(h => h.HoleId);

        // 5. Get hole stats
        var holeStats = await dbContext.HoleStats
            .Where(hs => roundIds.Contains(hs.RoundId) && !hs.IsDeleted)
            .ToListAsync();

        // Group scores and hole stats by round
        var scoresByRound = scores.GroupBy(s => s.RoundId).ToDictionary(g => g.Key, g => g.ToList());
        var holeStatsByRound = holeStats.GroupBy(hs => hs.RoundId).ToDictionary(g => g.Key, g => g.ToDictionary(hs => hs.HoleId));

        // Map to RoundResponse
        return roundsData.Select(x =>
        {
            var roundId = x.Round.RoundId;
            var roundScores = scoresByRound.GetValueOrDefault(roundId) ?? new();
            var roundHoleStats = holeStatsByRound.GetValueOrDefault(roundId) ?? new();

            var roundHoles = roundScores
                .Select(s => holes.GetValueOrDefault(s.HoleId))
                .Where(h => h != null)
                .OrderBy(h => h!.HoleNumber)
                .Select(h =>
                {
                    var score = roundScores.FirstOrDefault(s => s.HoleId == h!.HoleId);
                    var holeStat = roundHoleStats.GetValueOrDefault(h!.HoleId);
                    return RoundHole.From(h, score, holeStat);
                })
                .ToList();

            return RoundResponse.From(
                x.Round,
                x.Course,
                x.Teebox,
                roundStats.GetValueOrDefault(roundId),
                roundHoles
            );
        }).ToList();
    }

    public async Task<ScorecardDto?> GetRoundScorecardAsync(long roundId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Get round with course and teebox
        var roundData = await dbContext.Rounds
            .Where(r => r.RoundId == roundId && !r.IsDeleted)
            .Join(dbContext.Courses, r => r.CourseId, c => c.CourseId, (r, c) => new { Round = r, Course = c })
            .Join(dbContext.Teeboxes, rc => rc.Round.TeeboxId, t => t.TeeboxId, (rc, t) => new { rc.Round, rc.Course, Teebox = t })
            .FirstOrDefaultAsync();

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

        // Get hole stats for this round
        var holeStats = await dbContext.HoleStats
            .Where(hs => hs.RoundId == roundId && !hs.IsDeleted)
            .ToDictionaryAsync(hs => hs.HoleId);

        var scoresByHole = scores.ToDictionary(s => s.HoleId);

        // Build scorecard DTO
        return new ScorecardDto
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
            Holes = holes.Select(h => new ScorecardHoleDto
            {
                HoleId = h.HoleId,
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Yardage = h.Yardage,
                Handicap = h.Handicap,
                HoleScore = scoresByHole.GetValueOrDefault(h.HoleId)?.HoleScore,
                HitFairway = holeStats.GetValueOrDefault(h.HoleId)?.HitFairway,
                MissFairwayType = holeStats.GetValueOrDefault(h.HoleId)?.MissFairwayType,
                HitGreen = holeStats.GetValueOrDefault(h.HoleId)?.HitGreen,
                MissGreenType = holeStats.GetValueOrDefault(h.HoleId)?.MissGreenType,
                NumberOfPutts = holeStats.GetValueOrDefault(h.HoleId)?.NumberOfPutts
            }).ToList()
        };
    }
}
