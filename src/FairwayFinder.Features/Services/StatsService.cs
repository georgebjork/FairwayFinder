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

    public async Task<UserStatsResponse> GetUserStatsAsync(string userId, int trendCount = 20, int coursesCount = 5)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        // 1. Get all rounds with course names
        var roundsWithCourse = await (
            from r in dbContext.Rounds
            join c in dbContext.Courses on r.CourseId equals c.CourseId
            where r.UserId == userId && !r.IsDeleted && !r.ExcludeFromStats
            orderby r.DatePlayed descending
            select new
            {
                Round = r,
                CourseName = c.CourseName
            }
        ).ToListAsync();

        var result = new UserStatsResponse
        {
            TotalRounds = roundsWithCourse.Count
        };

        if (roundsWithCourse.Count == 0)
        {
            return result;
        }

        var roundIds = roundsWithCourse.Select(r => r.Round.RoundId).ToList();

        // 2. Get round stats for scoring distribution
        var roundStats = await dbContext.RoundStats
            .Where(rs => roundIds.Contains(rs.RoundId) && !rs.IsDeleted)
            .ToListAsync();

        // 3. Get scores with hole par for par type averages
        var scoresWithPar = await (
            from s in dbContext.Scores
            join h in dbContext.Holes on s.HoleId equals h.HoleId
            where roundIds.Contains(s.RoundId) && !s.IsDeleted
            select new { s.HoleScore, h.Par }
        ).ToListAsync();

        // 4. Get hole stats with par for advanced stats (only for rounds with UsingHoleStats)
        var advancedRoundIds = roundsWithCourse
            .Where(r => r.Round.UsingHoleStats)
            .Select(r => r.Round.RoundId)
            .ToList();

        var holeStatsWithPar = await (
            from hs in dbContext.HoleStats
            join h in dbContext.Holes on hs.HoleId equals h.HoleId
            where advancedRoundIds.Contains(hs.RoundId) && !hs.IsDeleted
            select new
            {
                hs.RoundId,
                hs.HitFairway,
                hs.HitGreen,
                hs.NumberOfPutts,
                h.Par
            }
        ).ToListAsync();

        // Now compute all stats in memory

        // Basic stats
        result.AverageScore = Math.Round(roundsWithCourse.Average(r => r.Round.Score), 1);

        // Average score trend (last 5 vs previous 5)
        if (roundsWithCourse.Count >= 10)
        {
            var last5Avg = Math.Round(roundsWithCourse.Take(5).Average(r => r.Round.Score), 1);
            var previous5Avg = Math.Round(roundsWithCourse.Skip(5).Take(5).Average(r => r.Round.Score), 1);
            result.AverageScoreTrend = Math.Round(last5Avg - previous5Avg, 1);
        }

        // Best round
        var best = roundsWithCourse.OrderBy(r => r.Round.Score).First();
        result.BestRound = new BestRound
        {
            RoundId = best.Round.RoundId,
            Score = best.Round.Score,
            CourseName = best.CourseName,
            DatePlayed = best.Round.DatePlayed
        };

        // Score trend (last N rounds, reversed for chart display oldest->newest)
        result.ScoreTrend = roundsWithCourse
            .Take(trendCount)
            .Reverse()
            .Select(r => new ScoreTrendPoint
            {
                RoundId = r.Round.RoundId,
                DatePlayed = r.Round.DatePlayed,
                Score = r.Round.Score,
                CourseName = r.CourseName
            })
            .ToList();

        // Most played courses
        result.MostPlayedCourses = roundsWithCourse
            .GroupBy(r => new { r.Round.CourseId, r.CourseName })
            .OrderByDescending(g => g.Count())
            .Take(coursesCount)
            .Select(g => new CourseStats
            {
                CourseId = g.Key.CourseId,
                CourseName = g.Key.CourseName,
                RoundCount = g.Count(),
                AverageScore = Math.Round(g.Average(r => (double)r.Round.Score), 1)
            })
            .ToList();

        // Scoring distribution (aggregate from RoundStats)
        if (roundStats.Count > 0)
        {
            result.ScoringDistribution = new ScoringDistribution
            {
                HolesInOne = roundStats.Sum(rs => rs.HoleInOne),
                DoubleEagles = roundStats.Sum(rs => rs.DoubleEagles),
                Eagles = roundStats.Sum(rs => rs.Eagles),
                Birdies = roundStats.Sum(rs => rs.Birdies),
                Pars = roundStats.Sum(rs => rs.Pars),
                Bogeys = roundStats.Sum(rs => rs.Bogies),
                DoubleBogeys = roundStats.Sum(rs => rs.DoubleBogies),
                TripleOrWorse = roundStats.Sum(rs => rs.TripleOrWorse),
                RoundCount = roundStats.Count
            };
        }

        // Par type scoring
        var par3Scores = scoresWithPar.Where(s => s.Par == 3).ToList();
        var par4Scores = scoresWithPar.Where(s => s.Par == 4).ToList();
        var par5Scores = scoresWithPar.Where(s => s.Par == 5).ToList();

        result.ParTypeScoring = new ParTypeScoring
        {
            Par3Average = par3Scores.Count > 0 ? Math.Round(par3Scores.Average(s => (double)s.HoleScore), 2) : null,
            Par3Count = par3Scores.Count,
            Par4Average = par4Scores.Count > 0 ? Math.Round(par4Scores.Average(s => (double)s.HoleScore), 2) : null,
            Par4Count = par4Scores.Count,
            Par5Average = par5Scores.Count > 0 ? Math.Round(par5Scores.Average(s => (double)s.HoleScore), 2) : null,
            Par5Count = par5Scores.Count
        };

        // Advanced stats (FIR, GIR, Putts)
        result.AdvancedStats = new AdvancedStats
        {
            RoundsWithStats = advancedRoundIds.Count
        };

        if (holeStatsWithPar.Count > 0)
        {
            // FIR % (only par 4/5 holes)
            var fairwayHoles = holeStatsWithPar.Where(h => h.Par > 3 && h.HitFairway.HasValue).ToList();
            if (fairwayHoles.Count > 0)
            {
                var fairwaysHit = fairwayHoles.Count(h => h.HitFairway == true);
                result.AdvancedStats.FirPercent = Math.Round((double)fairwaysHit / fairwayHoles.Count * 100, 1);
            }

            // GIR % (all holes)
            var greenHoles = holeStatsWithPar.Where(h => h.HitGreen.HasValue).ToList();
            if (greenHoles.Count > 0)
            {
                var greensHit = greenHoles.Count(h => h.HitGreen == true);
                result.AdvancedStats.GirPercent = Math.Round((double)greensHit / greenHoles.Count * 100, 1);
            }

            // Average putts per round
            var holesWithPutts = holeStatsWithPar.Where(h => h.NumberOfPutts.HasValue).ToList();
            if (holesWithPutts.Count > 0)
            {
                var puttsByRound = holesWithPutts
                    .GroupBy(h => h.RoundId)
                    .Select(g => g.Sum(h => h.NumberOfPutts!.Value))
                    .ToList();

                if (puttsByRound.Count > 0)
                {
                    result.AdvancedStats.AveragePutts = Math.Round(puttsByRound.Average(), 1);
                }
            }

            // Trend calculations (last 5 vs previous 5 rounds with advanced stats)
            if (advancedRoundIds.Count >= 10)
            {
                var recent5RoundIds = advancedRoundIds.Take(5).ToList();
                var previous5RoundIds = advancedRoundIds.Skip(5).Take(5).ToList();

                // FIR Trend
                var recent5FairwayHoles = holeStatsWithPar.Where(h => recent5RoundIds.Contains(h.RoundId) && h.Par > 3 && h.HitFairway.HasValue).ToList();
                var previous5FairwayHoles = holeStatsWithPar.Where(h => previous5RoundIds.Contains(h.RoundId) && h.Par > 3 && h.HitFairway.HasValue).ToList();

                if (recent5FairwayHoles.Count > 0 && previous5FairwayHoles.Count > 0)
                {
                    var recent5Fir = (double)recent5FairwayHoles.Count(h => h.HitFairway == true) / recent5FairwayHoles.Count * 100;
                    var previous5Fir = (double)previous5FairwayHoles.Count(h => h.HitFairway == true) / previous5FairwayHoles.Count * 100;
                    result.AdvancedStats.FirPercentTrend = Math.Round(recent5Fir - previous5Fir, 1);
                }

                // GIR Trend
                var recent5GreenHoles = holeStatsWithPar.Where(h => recent5RoundIds.Contains(h.RoundId) && h.HitGreen.HasValue).ToList();
                var previous5GreenHoles = holeStatsWithPar.Where(h => previous5RoundIds.Contains(h.RoundId) && h.HitGreen.HasValue).ToList();

                if (recent5GreenHoles.Count > 0 && previous5GreenHoles.Count > 0)
                {
                    var recent5Gir = (double)recent5GreenHoles.Count(h => h.HitGreen == true) / recent5GreenHoles.Count * 100;
                    var previous5Gir = (double)previous5GreenHoles.Count(h => h.HitGreen == true) / previous5GreenHoles.Count * 100;
                    result.AdvancedStats.GirPercentTrend = Math.Round(recent5Gir - previous5Gir, 1);
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
                        result.AdvancedStats.AveragePuttsTrend = Math.Round(recent5AvgPutts - previous5AvgPutts, 1);
                    }
                }
            }
        }

        return result;
    }
}
