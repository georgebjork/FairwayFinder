using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;
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
            UsingHoleStats = request.UsingHoleStats || request.UsingShotTracking,
            UsingShotTracking = request.UsingShotTracking,
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
        
        // 3. Create Shot entities if shot tracking enabled
        if (request.UsingShotTracking)
        {
            var scoreByHoleId = scores.ToDictionary(s => s.HoleId, s => s.ScoreId);

            foreach (var hole in request.Holes)
            {
                if (hole.Shots is not { Count: > 0 }) continue;

                var scoreId = scoreByHoleId[hole.HoleId];
                var shotNumber = 1;
                foreach (var shotData in hole.Shots)
                {
                    dbContext.Shots.Add(new Shot
                    {
                        ScoreId = scoreId,
                        ShotNumber = shotNumber++,
                        StartDistance = shotData.StartDistance,
                        StartDistanceUnit = shotData.StartDistanceUnit,
                        StartLie = (int)shotData.StartLie,
                        EndDistance = shotData.EndDistance,
                        EndDistanceUnit = shotData.EndDistanceUnit,
                        EndLie = shotData.EndLie.HasValue ? (int)shotData.EndLie.Value : null,
                        PenaltyStrokes = shotData.PenaltyStrokes,
                        CreatedBy = userId,
                        CreatedOn = today,
                        UpdatedBy = userId,
                        UpdatedOn = today,
                        IsDeleted = false
                    });
                }

                // Auto-derive HoleStat from shots
                var (numberOfPutts, hitFairway, hitGreen, approachYardage, teeShotOb, approachShotOb) =
                    StrokesGainedCalculator.DeriveHoleStatFromShots(hole.Shots, hole.Par);

                dbContext.HoleStats.Add(new HoleStat
                {
                    ScoreId = scoreId,
                    RoundId = round.RoundId,
                    HoleId = hole.HoleId,
                    HitFairway = hitFairway,
                    HitGreen = hitGreen,
                    NumberOfPutts = numberOfPutts,
                    ApproachYardage = approachYardage,
                    TeeShotOb = teeShotOb,
                    ApproachShotOb = approachShotOb,
                    CreatedBy = userId,
                    CreatedOn = today,
                    UpdatedBy = userId,
                    UpdatedOn = today,
                    IsDeleted = false
                });
            }
        }
        // 3b. Create HoleStat entities if advanced stats enabled (non-shot-tracking)
        else if (request.UsingHoleStats)
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
                ApproachYardage = h.ApproachYardage,
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
    
    public async Task<bool> UpdateRoundAsync(UpdateRoundRequest request)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = request.UserId;
        
        var round = await dbContext.Rounds
            .FirstOrDefaultAsync(r => r.RoundId == request.RoundId && !r.IsDeleted);
        
        if (round is null || round.UserId != userId)
        {
            return false;
        }
        
        var frontHoles = request.Holes.Where(h => h.HoleNumber <= 9).ToList();
        var backHoles = request.Holes.Where(h => h.HoleNumber > 9).ToList();
        var scoreOut = frontHoles.Sum(h => (int)h.Score);
        var scoreIn = backHoles.Sum(h => (int)h.Score);
        var totalScore = scoreOut + scoreIn;
        
        round.TeeboxId = request.TeeboxId;
        round.DatePlayed = request.DatePlayed;
        round.UsingHoleStats = request.UsingHoleStats || request.UsingShotTracking;
        round.UsingShotTracking = request.UsingShotTracking;
        round.Score = totalScore;
        round.ScoreOut = scoreOut;
        round.ScoreIn = scoreIn;
        round.UpdatedBy = userId;
        round.UpdatedOn = today;
        
        var existingScores = await dbContext.Scores
            .Where(s => s.RoundId == request.RoundId && !s.IsDeleted)
            .ToListAsync();
        
        var scoresByScoreId = existingScores.ToDictionary(s => s.ScoreId);
        
        // Track the Score entity for each hole entry (needed for HoleStats ScoreId lookup)
        var scoreForHole = new Dictionary<long, Score>(); // keyed by HoleId from request
        
        foreach (var hole in request.Holes)
        {
            if (hole.ScoreId > 0 && scoresByScoreId.TryGetValue(hole.ScoreId, out var existingScore))
            {
                existingScore.HoleId = hole.HoleId; // Update HoleId in case teebox changed
                existingScore.HoleScore = hole.Score;
                existingScore.UpdatedBy = userId;
                existingScore.UpdatedOn = today;
                scoreForHole[hole.HoleId] = existingScore;
            }
            else
            {
                var newScore = new Score
                {
                    RoundId = round.RoundId,
                    HoleId = hole.HoleId,
                    HoleScore = hole.Score,
                    UserId = userId,
                    CreatedBy = userId,
                    CreatedOn = today,
                    UpdatedBy = userId,
                    UpdatedOn = today,
                    IsDeleted = false
                };
                dbContext.Scores.Add(newScore);
                scoreForHole[hole.HoleId] = newScore;
            }
        }
        
        await dbContext.SaveChangesAsync(); // Generates ScoreIds for any new scores
        
        var existingHoleStats = await dbContext.HoleStats
            .Where(hs => hs.RoundId == request.RoundId && !hs.IsDeleted)
            .ToListAsync();
        
        var holeStatsByScoreId = existingHoleStats.ToDictionary(hs => hs.ScoreId);
        
        if (request.UsingShotTracking)
        {
            // Delete old shots for this round
            var scoreIds = scoreForHole.Values.Select(s => s.ScoreId).ToList();
            var existingShots = await dbContext.Shots
                .Where(s => scoreIds.Contains(s.ScoreId) && !s.IsDeleted)
                .ToListAsync();
            foreach (var shot in existingShots)
            {
                shot.IsDeleted = true;
                shot.UpdatedBy = userId;
                shot.UpdatedOn = today;
            }

            // Insert new shots and auto-derive HoleStats
            foreach (var hole in request.Holes)
            {
                if (hole.Shots is not { Count: > 0 }) continue;

                var score = scoreForHole[hole.HoleId];
                var shotNumber = 1;
                foreach (var shotData in hole.Shots)
                {
                    dbContext.Shots.Add(new Shot
                    {
                        ScoreId = score.ScoreId,
                        ShotNumber = shotNumber++,
                        StartDistance = shotData.StartDistance,
                        StartDistanceUnit = shotData.StartDistanceUnit,
                        StartLie = (int)shotData.StartLie,
                        EndDistance = shotData.EndDistance,
                        EndDistanceUnit = shotData.EndDistanceUnit,
                        EndLie = shotData.EndLie.HasValue ? (int)shotData.EndLie.Value : null,
                        PenaltyStrokes = shotData.PenaltyStrokes,
                        CreatedBy = userId,
                        CreatedOn = today,
                        UpdatedBy = userId,
                        UpdatedOn = today,
                        IsDeleted = false
                    });
                }

                // Auto-derive HoleStat from shots
                var (numberOfPutts, hitFairway, hitGreen, approachYardage, teeShotOb, approachShotOb) =
                    StrokesGainedCalculator.DeriveHoleStatFromShots(hole.Shots, hole.Par);

                if (holeStatsByScoreId.TryGetValue(score.ScoreId, out var existingStat))
                {
                    existingStat.HoleId = hole.HoleId;
                    existingStat.HitFairway = hitFairway;
                    existingStat.HitGreen = hitGreen;
                    existingStat.NumberOfPutts = numberOfPutts;
                    existingStat.ApproachYardage = approachYardage;
                    existingStat.TeeShotOb = teeShotOb;
                    existingStat.ApproachShotOb = approachShotOb;
                    existingStat.UpdatedBy = userId;
                    existingStat.UpdatedOn = today;
                }
                else
                {
                    dbContext.HoleStats.Add(new HoleStat
                    {
                        ScoreId = score.ScoreId,
                        RoundId = round.RoundId,
                        HoleId = hole.HoleId,
                        HitFairway = hitFairway,
                        HitGreen = hitGreen,
                        NumberOfPutts = numberOfPutts,
                        ApproachYardage = approachYardage,
                        TeeShotOb = teeShotOb,
                        ApproachShotOb = approachShotOb,
                        CreatedBy = userId,
                        CreatedOn = today,
                        UpdatedBy = userId,
                        UpdatedOn = today,
                        IsDeleted = false
                    });
                }
            }
        }
        else if (request.UsingHoleStats)
        {
            foreach (var hole in request.Holes)
            {
                var score = scoreForHole[hole.HoleId];

                if (holeStatsByScoreId.TryGetValue(score.ScoreId, out var existingStat))
                {
                    existingStat.HoleId = hole.HoleId;
                    existingStat.HitFairway = hole.HitFairway;
                    existingStat.MissFairwayType = hole.MissFairwayType;
                    existingStat.HitGreen = hole.HitGreen;
                    existingStat.MissGreenType = hole.MissGreenType;
                    existingStat.NumberOfPutts = hole.NumberOfPutts;
                    existingStat.ApproachYardage = hole.ApproachYardage;
                    existingStat.UpdatedBy = userId;
                    existingStat.UpdatedOn = today;
                }
                else
                {
                    dbContext.HoleStats.Add(new HoleStat
                    {
                        ScoreId = score.ScoreId,
                        RoundId = round.RoundId,
                        HoleId = hole.HoleId,
                        HitFairway = hole.HitFairway,
                        MissFairwayType = hole.MissFairwayType,
                        HitGreen = hole.HitGreen,
                        MissGreenType = hole.MissGreenType,
                        NumberOfPutts = hole.NumberOfPutts,
                        ApproachYardage = hole.ApproachYardage,
                        CreatedBy = userId,
                        CreatedOn = today,
                        UpdatedBy = userId,
                        UpdatedOn = today,
                        IsDeleted = false
                    });
                }
            }
        }
        else
        {
            foreach (var hs in existingHoleStats)
            {
                hs.IsDeleted = true;
                hs.UpdatedBy = userId;
                hs.UpdatedOn = today;
            }
        }
        
        int holeInOne = 0, doubleEagles = 0, eagles = 0, birdies = 0, pars = 0, bogies = 0, doubleBogies = 0, tripleOrWorse = 0;
        
        foreach (var hole in request.Holes)
        {
            var diff = hole.Score - hole.Par;
            switch (diff)
            {
                case <= -3:
                    if (hole.Score == 1) holeInOne++;
                    else doubleEagles++;
                    break;
                case -2: eagles++; break;
                case -1: birdies++; break;
                case 0: pars++; break;
                case 1: bogies++; break;
                case 2: doubleBogies++; break;
                default: tripleOrWorse++; break;
            }
        }
        
        var existingRoundStat = await dbContext.RoundStats
            .FirstOrDefaultAsync(rs => rs.RoundId == request.RoundId && !rs.IsDeleted);
        
        if (existingRoundStat is not null)
        {
            existingRoundStat.HoleInOne = holeInOne;
            existingRoundStat.DoubleEagles = doubleEagles;
            existingRoundStat.Eagles = eagles;
            existingRoundStat.Birdies = birdies;
            existingRoundStat.Pars = pars;
            existingRoundStat.Bogies = bogies;
            existingRoundStat.DoubleBogies = doubleBogies;
            existingRoundStat.TripleOrWorse = tripleOrWorse;
            existingRoundStat.UpdatedBy = userId;
            existingRoundStat.UpdatedOn = today;
        }
        else
        {
            dbContext.RoundStats.Add(new RoundStat
            {
                RoundId = round.RoundId,
                HoleInOne = holeInOne,
                DoubleEagles = doubleEagles,
                Eagles = eagles,
                Birdies = birdies,
                Pars = pars,
                Bogies = bogies,
                DoubleBogies = doubleBogies,
                TripleOrWorse = tripleOrWorse,
                CreatedBy = userId,
                CreatedOn = today,
                UpdatedBy = userId,
                UpdatedOn = today,
                IsDeleted = false
            });
        }
        
        await dbContext.SaveChangesAsync();
        
        return true;
    }
    
    public async Task<bool> DeleteRoundAsync(long roundId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var round = await dbContext.Rounds
            .FirstOrDefaultAsync(r => r.RoundId == roundId && !r.IsDeleted);

        if (round is null) return false;

        // Soft-delete the round
        round.IsDeleted = true;
        round.UpdatedBy = userId;
        round.UpdatedOn = now;

        // Soft-delete all scores for this round
        var scores = await dbContext.Scores
            .Where(s => s.RoundId == roundId && !s.IsDeleted)
            .ToListAsync();

        foreach (var score in scores)
        {
            score.IsDeleted = true;
            score.UpdatedBy = userId;
            score.UpdatedOn = now;
        }

        // Soft-delete all hole stats for this round
        var holeStats = await dbContext.HoleStats
            .Where(hs => hs.RoundId == roundId && !hs.IsDeleted)
            .ToListAsync();

        foreach (var hs in holeStats)
        {
            hs.IsDeleted = true;
            hs.UpdatedBy = userId;
            hs.UpdatedOn = now;
        }

        // Soft-delete all shots for this round
        var scoreIds = scores.Select(s => s.ScoreId).ToList();
        var shots = await dbContext.Shots
            .Where(s => scoreIds.Contains(s.ScoreId) && !s.IsDeleted)
            .ToListAsync();

        foreach (var shot in shots)
        {
            shot.IsDeleted = true;
            shot.UpdatedBy = userId;
            shot.UpdatedOn = now;
        }

        // Soft-delete the round stat
        var roundStat = await dbContext.RoundStats
            .FirstOrDefaultAsync(rs => rs.RoundId == roundId && !rs.IsDeleted);

        if (roundStat is not null)
        {
            roundStat.IsDeleted = true;
            roundStat.UpdatedBy = userId;
            roundStat.UpdatedOn = now;
        }

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsRoundOwnedByUserAsync(long roundId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Rounds
            .AnyAsync(r => r.RoundId == roundId && r.UserId == userId && !r.IsDeleted);
    }

    public async Task<List<CourseResponse>> GetPlayedCoursesByUserId(string userId, bool? statRounds = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        List<Course> courses;
        if (!statRounds.HasValue)
        {
            courses = await dbContext.Courses
                .Join(dbContext.Rounds.Where(r => r.UserId == userId && !r.IsDeleted),
                    c => c.CourseId,
                    r => r.CourseId,
                    (c, r) => c)
                .Distinct()
                .ToListAsync();
        }
        else
        {
            courses = await dbContext.Courses
                .Join(dbContext.Rounds.Where(r => r.UserId == userId && !r.IsDeleted && r.UsingHoleStats == statRounds),
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

    public async Task<Dictionary<long, List<ShotData>>> GetShotsByRoundIdAsync(long roundId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var scoreIds = await dbContext.Scores
            .Where(s => s.RoundId == roundId && !s.IsDeleted)
            .Select(s => s.ScoreId)
            .ToListAsync();

        var shots = await dbContext.Shots
            .Where(s => scoreIds.Contains(s.ScoreId) && !s.IsDeleted)
            .OrderBy(s => s.ScoreId).ThenBy(s => s.ShotNumber)
            .ToListAsync();

        return shots.GroupBy(s => s.ScoreId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => new ShotData
                {
                    ShotId = s.ShotId,
                    ShotNumber = s.ShotNumber,
                    StartDistance = s.StartDistance,
                    StartDistanceUnit = s.StartDistanceUnit,
                    StartLie = (LieType)s.StartLie,
                    EndDistance = s.EndDistance,
                    EndDistanceUnit = s.EndDistanceUnit,
                    EndLie = s.EndLie.HasValue ? (LieType)s.EndLie.Value : null,
                    PenaltyStrokes = s.PenaltyStrokes
                }).ToList()
            );
    }

    public async Task LoadShotsForRoundsAsync(List<RoundResponse> rounds)
    {
        var shotTrackedRounds = rounds.Where(r => r.UsingShotTracking).ToList();
        if (shotTrackedRounds.Count == 0) return;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var roundIds = shotTrackedRounds.Select(r => r.RoundId).ToList();

        // Get all scores for these rounds
        var scores = await dbContext.Scores
            .Where(s => roundIds.Contains(s.RoundId) && !s.IsDeleted)
            .ToListAsync();

        var scoreIds = scores.Select(s => s.ScoreId).ToList();

        // Get all shots for these scores
        var shots = await dbContext.Shots
            .Where(s => scoreIds.Contains(s.ScoreId) && !s.IsDeleted)
            .OrderBy(s => s.ShotNumber)
            .ToListAsync();

        var shotsByScoreId = shots.GroupBy(s => s.ScoreId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => new ShotData
                {
                    ShotId = s.ShotId,
                    ShotNumber = s.ShotNumber,
                    StartDistance = s.StartDistance,
                    StartDistanceUnit = s.StartDistanceUnit,
                    StartLie = (LieType)s.StartLie,
                    EndDistance = s.EndDistance,
                    EndDistanceUnit = s.EndDistanceUnit,
                    EndLie = s.EndLie.HasValue ? (LieType)s.EndLie.Value : null,
                    PenaltyStrokes = s.PenaltyStrokes
                }).ToList()
            );

        // Attach shots to round holes
        foreach (var round in shotTrackedRounds)
        {
            foreach (var hole in round.Holes)
            {
                if (hole.ScoreId > 0 && shotsByScoreId.TryGetValue(hole.ScoreId, out var holeShots))
                {
                    hole.Shots = holeShots;
                }
            }
        }
    }
}
