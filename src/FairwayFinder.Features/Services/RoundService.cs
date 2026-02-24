using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
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

    public async Task<RoundResponse?> GetRoundByIdAsync(long roundId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // 1. Get round with course and teebox
        var roundData = await dbContext.Rounds
            .Where(r => r.RoundId == roundId && !r.IsDeleted)
            .Join(dbContext.Courses, r => r.CourseId, c => c.CourseId, (r, c) => new { Round = r, Course = c })
            .Join(dbContext.Teeboxes, rc => rc.Round.TeeboxId, t => t.TeeboxId, (rc, t) => new { rc.Round, rc.Course, Teebox = t })
            .FirstOrDefaultAsync();

        if (roundData is null)
        {
            return null;
        }

        // 2. Get round stats
        var roundStat = await dbContext.RoundStats
            .Where(rs => rs.RoundId == roundId && !rs.IsDeleted)
            .FirstOrDefaultAsync();

        // 3. Get scores
        var scores = await dbContext.Scores
            .Where(s => s.RoundId == roundId && !s.IsDeleted)
            .ToListAsync();

        // 4. Get holes for all scores
        var holeIds = scores.Select(s => s.HoleId).Distinct().ToList();
        var holes = await dbContext.Holes
            .Where(h => holeIds.Contains(h.HoleId) && !h.IsDeleted)
            .ToDictionaryAsync(h => h.HoleId);

        // 5. Get hole stats
        var holeStats = await dbContext.HoleStats
            .Where(hs => hs.RoundId == roundId && !hs.IsDeleted)
            .ToDictionaryAsync(hs => hs.HoleId);

        // Build hole list
        var roundHoles = scores
            .Select(s => holes.GetValueOrDefault(s.HoleId))
            .Where(h => h != null)
            .OrderBy(h => h!.HoleNumber)
            .Select(h =>
            {
                var score = scores.FirstOrDefault(s => s.HoleId == h!.HoleId);
                var holeStat = holeStats.GetValueOrDefault(h!.HoleId);
                return RoundHole.From(h, score, holeStat);
            })
            .ToList();

        return RoundResponse.From(
            roundData.Round,
            roundData.Course,
            roundData.Teebox,
            roundStat,
            roundHoles
        );
    }
    
    public async Task<long> CreateRoundAsync(CreateRoundRequest request)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = request.UserId;
        
        // Compute scores
        var frontHoles = request.Holes.Where(h => h.HoleNumber <= 9).ToList();
        var backHoles = request.Holes.Where(h => h.HoleNumber > 9).ToList();
        var scoreOut = frontHoles.Sum(h => (int)h.Score);
        var scoreIn = backHoles.Sum(h => (int)h.Score);
        var totalScore = scoreOut + scoreIn;
        
        // 1. Create Round entity
        var round = new Round
        {
            CourseId = request.CourseId,
            TeeboxId = request.TeeboxId,
            DatePlayed = request.DatePlayed,
            Score = totalScore,
            ScoreOut = scoreOut,
            ScoreIn = scoreIn,
            UserId = userId,
            UsingHoleStats = request.UsingHoleStats,
            ExcludeFromStats = false,
            FullRound = request.FullRound,
            FrontNine = request.FrontNine,
            BackNine = request.BackNine,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today,
            IsDeleted = false
        };
        
        dbContext.Rounds.Add(round);
        await dbContext.SaveChangesAsync(); // Save to get RoundId
        
        // 2. Create Score entities for each hole
        var scores = request.Holes.Select(h => new Score
        {
            RoundId = round.RoundId,
            HoleId = h.HoleId,
            HoleScore = h.Score,
            UserId = userId,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today,
            IsDeleted = false
        }).ToList();
        
        dbContext.Scores.AddRange(scores);
        await dbContext.SaveChangesAsync(); // Save to get ScoreIds
        
        // 3. Create HoleStat entities if advanced stats enabled
        if (request.UsingHoleStats)
        {
            var scoreByHoleId = scores.ToDictionary(s => s.HoleId, s => s.ScoreId);
            
            var holeStats = request.Holes.Select(h => new HoleStat
            {
                ScoreId = scoreByHoleId[h.HoleId],
                RoundId = round.RoundId,
                HoleId = h.HoleId,
                HitFairway = h.HitFairway,
                MissFairwayType = h.MissFairwayType,
                HitGreen = h.HitGreen,
                MissGreenType = h.MissGreenType,
                NumberOfPutts = h.NumberOfPutts,
                CreatedBy = userId,
                CreatedOn = today,
                UpdatedBy = userId,
                UpdatedOn = today,
                IsDeleted = false
            }).ToList();
            
            dbContext.HoleStats.AddRange(holeStats);
        }
        
        // 4. Compute and create RoundStat (scoring distribution)
        var roundStat = new RoundStat
        {
            RoundId = round.RoundId,
            HoleInOne = 0,
            DoubleEagles = 0,
            Eagles = 0,
            Birdies = 0,
            Pars = 0,
            Bogies = 0,
            DoubleBogies = 0,
            TripleOrWorse = 0,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today,
            IsDeleted = false
        };
        
        foreach (var hole in request.Holes)
        {
            var diff = hole.Score - hole.Par;
            switch (diff)
            {
                case <= -3:
                    if (hole.Score == 1) roundStat.HoleInOne++;
                    else roundStat.DoubleEagles++;
                    break;
                case -2:
                    roundStat.Eagles++;
                    break;
                case -1:
                    roundStat.Birdies++;
                    break;
                case 0:
                    roundStat.Pars++;
                    break;
                case 1:
                    roundStat.Bogies++;
                    break;
                case 2:
                    roundStat.DoubleBogies++;
                    break;
                default:
                    roundStat.TripleOrWorse++;
                    break;
            }
        }
        
        dbContext.RoundStats.Add(roundStat);
        await dbContext.SaveChangesAsync();
        
        return round.RoundId;
    }
    
    public async Task<List<CourseResponse>> GetPlayedCoursesByUserId(string userId, bool? statRounds = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        List<Course> courses;
        if (!statRounds.HasValue)
        {
            courses = await dbContext.Courses
                .Join(dbContext.Rounds.Where(r => r.UserId == userId),
                    c => c.CourseId,
                    r => r.CourseId,
                    (c, r) => c)
                .Distinct()
                .ToListAsync();
        }
        else
        {
            courses = await dbContext.Courses
                .Join(dbContext.Rounds.Where(r => r.UserId == userId && r.UsingHoleStats == statRounds),
                    c => c.CourseId,
                    r => r.CourseId,
                    (c, r) => c)
                .Distinct()
                .ToListAsync();
        }

        return courses.Select(x => new CourseResponse
        {
            CourseId = x.CourseId,
            CourseName = x.CourseName
        }).ToList();

    }
}
