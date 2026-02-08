using FairwayFinder.Data;
using FairwayFinder.Features.Data;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class StatsService : IStatsService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public StatsService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        // Get all rounds for this user (excluding deleted and excluded from stats)
        var rounds = await dbContext.Rounds
            .Where(r => r.UserId == userId && !r.IsDeleted && !r.ExcludeFromStats)
            .ToListAsync();

        var stats = new DashboardStatsDto
        {
            TotalRounds = rounds.Count
        };

        if (rounds.Count > 0)
        {
            stats.AverageScore = Math.Round(rounds.Average(r => r.Score), 1);

            if (rounds.Count > 10)
            {
                var previous5Average = Math.Round(rounds.Skip(10).Take(5).Average(r => r.Score), 1);
                var last5Average = Math.Round(rounds.Skip(15).Take(5).Average(r => r.Score), 1);
                stats.AverageScoreTrend = Math.Round(last5Average - previous5Average, 1);
            }

            var bestRound = rounds.OrderBy(r => r.Score).First();
            
            var course = await dbContext.Courses
                .Where(c => c.CourseId == bestRound.CourseId)
                .FirstOrDefaultAsync();

            stats.BestRound = new BestRoundDto
            {
                RoundId = bestRound.RoundId,
                Score = bestRound.Score,
                CourseName = course?.CourseName ?? "Unknown",
                DatePlayed = bestRound.DatePlayed
            };
        }

        return stats;
    }

    public async Task<List<ScoreTrendPointDto>> GetScoreTrendAsync(string userId, int count = 20)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        // Get the last N rounds ordered by date descending, then reverse for chart display
        var trendData = await (
            from round in dbContext.Rounds
            join course in dbContext.Courses on round.CourseId equals course.CourseId
            where round.UserId == userId && !round.IsDeleted && !round.ExcludeFromStats
            orderby round.DatePlayed descending
            select new ScoreTrendPointDto
            {
                RoundId = round.RoundId,
                DatePlayed = round.DatePlayed,
                Score = round.Score,
                CourseName = course.CourseName
            })
            .Take(count)
            .ToListAsync();

        // Reverse to show oldest first (left to right on chart)
        trendData.Reverse();
        
        return trendData;
    }

    public async Task<AdvancedStatsDto> GetAdvancedStatsAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        // Get all rounds with advanced stats for this user, ordered by date descending
        var roundsWithStats = await dbContext.Rounds
            .Where(r => r.UserId == userId && !r.IsDeleted && !r.ExcludeFromStats && r.UsingHoleStats)
            .OrderByDescending(r => r.DatePlayed)
            .Select(r => r.RoundId)
            .ToListAsync();

        var result = new AdvancedStatsDto
        {
            RoundsWithStats = roundsWithStats.Count
        };

        if (roundsWithStats.Count == 0)
        {
            return result;
        }

        // Get all hole stats for these rounds, joined with holes to get par info
        var holeStatsWithPar = await (
            from holeStat in dbContext.HoleStats
            join hole in dbContext.Holes on holeStat.HoleId equals hole.HoleId
            where roundsWithStats.Contains(holeStat.RoundId) && !holeStat.IsDeleted
            select new
            {
                holeStat.RoundId,
                holeStat.HitFairway,
                holeStat.HitGreen,
                holeStat.NumberOfPutts,
                hole.Par
            })
            .ToListAsync();

        // Calculate FIR % (only par 4/5 holes count)
        var fairwayHoles = holeStatsWithPar.Where(h => h.Par > 3 && h.HitFairway.HasValue).ToList();
        if (fairwayHoles.Count > 0)
        {
            var fairwaysHit = fairwayHoles.Count(h => h.HitFairway == true);
            result.FirPercent = Math.Round((double)fairwaysHit / fairwayHoles.Count * 100, 1);
        }

        // Calculate GIR % (all holes count)
        var greenHoles = holeStatsWithPar.Where(h => h.HitGreen.HasValue).ToList();
        if (greenHoles.Count > 0)
        {
            var greensHit = greenHoles.Count(h => h.HitGreen == true);
            result.GirPercent = Math.Round((double)greensHit / greenHoles.Count * 100, 1);
        }

        // Calculate average putts per round
        var holesWithPutts = holeStatsWithPar.Where(h => h.NumberOfPutts.HasValue).ToList();
        if (holesWithPutts.Count > 0)
        {
            // Group by round and sum putts, then average across rounds
            var puttsByRound = holesWithPutts
                .GroupBy(h => h.RoundId)
                .Select(g => g.Sum(h => h.NumberOfPutts!.Value))
                .ToList();
            
            if (puttsByRound.Count > 0)
            {
                result.AveragePutts = Math.Round(puttsByRound.Average(), 1);
            }
        }

        // Calculate trends if we have at least 10 rounds
        if (roundsWithStats.Count >= 10)
        {
            var recent5RoundIds = roundsWithStats.Take(5).ToList();
            var previous5RoundIds = roundsWithStats.Skip(5).Take(5).ToList();
            
            // FIR Trend
            var recent5FairwayHoles = holeStatsWithPar.Where(h => recent5RoundIds.Contains(h.RoundId) && h.Par > 3 && h.HitFairway.HasValue).ToList();
            var previous5FairwayHoles = holeStatsWithPar.Where(h => previous5RoundIds.Contains(h.RoundId) && h.Par > 3 && h.HitFairway.HasValue).ToList();
            
            if (recent5FairwayHoles.Count > 0 && previous5FairwayHoles.Count > 0)
            {
                var recent5Fir = (double)recent5FairwayHoles.Count(h => h.HitFairway == true) / recent5FairwayHoles.Count * 100;
                var previous5Fir = (double)previous5FairwayHoles.Count(h => h.HitFairway == true) / previous5FairwayHoles.Count * 100;
                result.FirPercentTrend = Math.Round(recent5Fir - previous5Fir, 1);
            }
            
            // GIR Trend
            var recent5GreenHoles = holeStatsWithPar.Where(h => recent5RoundIds.Contains(h.RoundId) && h.HitGreen.HasValue).ToList();
            var previous5GreenHoles = holeStatsWithPar.Where(h => previous5RoundIds.Contains(h.RoundId) && h.HitGreen.HasValue).ToList();
            
            if (recent5GreenHoles.Count > 0 && previous5GreenHoles.Count > 0)
            {
                var recent5Gir = (double)recent5GreenHoles.Count(h => h.HitGreen == true) / recent5GreenHoles.Count * 100;
                var previous5Gir = (double)previous5GreenHoles.Count(h => h.HitGreen == true) / previous5GreenHoles.Count * 100;
                result.GirPercentTrend = Math.Round(recent5Gir - previous5Gir, 1);
            }
            
            // Putts Trend
            var recent5PuttHoles = holeStatsWithPar.Where(h => recent5RoundIds.Contains(h.RoundId) && h.NumberOfPutts.HasValue).ToList();
            var previous5PuttHoles = holeStatsWithPar.Where(h => previous5RoundIds.Contains(h.RoundId) && h.NumberOfPutts.HasValue).ToList();
            
            if (recent5PuttHoles.Count > 0 && previous5PuttHoles.Count > 0)
            {
                var recent5PuttsByRound = recent5PuttHoles.GroupBy(h => h.RoundId).Select(g => g.Sum(h => h.NumberOfPutts!.Value)).ToList();
                var previous5PuttsByRound = previous5PuttHoles.GroupBy(h => h.RoundId).Select(g => g.Sum(h => h.NumberOfPutts!.Value)).ToList();
                
                if (recent5PuttsByRound.Count > 0 && previous5PuttsByRound.Count > 0)
                {
                    var recent5AvgPutts = recent5PuttsByRound.Average();
                    var previous5AvgPutts = previous5PuttsByRound.Average();
                    result.AveragePuttsTrend = Math.Round(recent5AvgPutts - previous5AvgPutts, 1);
                }
            }
        }

        return result;
    }

    public async Task<ScoringDistributionDto> GetScoringDistributionAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        // Get all RoundStats for user's rounds, aggregated
        var distribution = await (
            from round in dbContext.Rounds
            join roundStat in dbContext.RoundStats on round.RoundId equals roundStat.RoundId
            where round.UserId == userId && !round.IsDeleted && !round.ExcludeFromStats && !roundStat.IsDeleted
            group roundStat by 1 into g
            select new ScoringDistributionDto
            {
                HolesInOne = g.Sum(rs => rs.HoleInOne),
                DoubleEagles = g.Sum(rs => rs.DoubleEagles),
                Eagles = g.Sum(rs => rs.Eagles),
                Birdies = g.Sum(rs => rs.Birdies),
                Pars = g.Sum(rs => rs.Pars),
                Bogeys = g.Sum(rs => rs.Bogies),
                DoubleBogeys = g.Sum(rs => rs.DoubleBogies),
                TripleOrWorse = g.Sum(rs => rs.TripleOrWorse),
                RoundCount = g.Count()
            })
            .FirstOrDefaultAsync();

        return distribution ?? new ScoringDistributionDto();
    }

    public async Task<List<MostPlayedCourseDto>> GetMostPlayedCoursesAsync(string userId, int count = 5)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var courses = await (
            from round in dbContext.Rounds
            join course in dbContext.Courses on round.CourseId equals course.CourseId
            where round.UserId == userId && !round.IsDeleted && !round.ExcludeFromStats
            group round by new { round.CourseId, course.CourseName } into g
            orderby g.Count() descending
            select new MostPlayedCourseDto
            {
                CourseId = g.Key.CourseId,
                CourseName = g.Key.CourseName,
                RoundCount = g.Count(),
                AverageScore = Math.Round(g.Average(r => (double)r.Score), 1)
            })
            .Take(count)
            .ToListAsync();

        return courses;
    }

    public async Task<ParTypeScoringDto> GetParTypeScoringAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        // Get all scores for user's rounds, joined with holes to get par
        var scoresWithPar = await (
            from score in dbContext.Scores
            join hole in dbContext.Holes on score.HoleId equals hole.HoleId
            join round in dbContext.Rounds on score.RoundId equals round.RoundId
            where round.UserId == userId && !round.IsDeleted && !round.ExcludeFromStats && !score.IsDeleted
            select new
            {
                score.HoleScore,
                hole.Par
            })
            .ToListAsync();

        var result = new ParTypeScoringDto();

        // Calculate Par 3 average
        var par3Scores = scoresWithPar.Where(s => s.Par == 3).ToList();
        if (par3Scores.Count > 0)
        {
            result.Par3Average = Math.Round(par3Scores.Average(s => (double)s.HoleScore), 2);
            result.Par3Count = par3Scores.Count;
        }

        // Calculate Par 4 average
        var par4Scores = scoresWithPar.Where(s => s.Par == 4).ToList();
        if (par4Scores.Count > 0)
        {
            result.Par4Average = Math.Round(par4Scores.Average(s => (double)s.HoleScore), 2);
            result.Par4Count = par4Scores.Count;
        }

        // Calculate Par 5 average
        var par5Scores = scoresWithPar.Where(s => s.Par == 5).ToList();
        if (par5Scores.Count > 0)
        {
            result.Par5Average = Math.Round(par5Scores.Average(s => (double)s.HoleScore), 2);
            result.Par5Count = par5Scores.Count;
        }

        return result;
    }
}
