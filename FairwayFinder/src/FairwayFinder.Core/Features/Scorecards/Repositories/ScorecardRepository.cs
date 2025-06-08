using Dapper;
using Dapper.Contrib.Extensions;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Scorecards.Repositories;

public interface IScorecardRepository : IBaseRepository
{
    Task<List<ScorecardSummaryQueryModel>> GetScorecardListByUserIdAsync(string userId, int? limit = null);
    Task<List<HoleScoreQueryModel>> GetHoleScoresByRoundIdAsync(long roundId);
    Task<List<Score>> GetScoresForRoundByIdAsync(long roundId);
    Task<RoundStats?> GetRoundStatsByIdAsync(long roundId);
    Task<Round?> GetRoundByIdAsync(long roundId);
    Task<List<HoleStats>> GetHoleStatsByRoundAsync(long roundId);
    Task<List<HoleStatsQueryModel>> GetHoleStatsByRoundIdAsync(long roundId);
    
    public Task<int> CreateNewScorecardAsync(Round round, List<Score> scores, RoundStats stats, List<HoleStats> holeStats);
    public Task<bool> UpdateScorecardAsync(Round round, List<Score> scores, RoundStats roundStats, List<HoleStats> holeStats);
}

public class ScorecardRepository(IConfiguration configuration, ILogger<IScorecardRepository> logger) : BasePgRepository(configuration), IScorecardRepository
{
    public async Task<int> CreateNewScorecardAsync(Round round, List<Score> scores, RoundStats stats, List<HoleStats> holeStats)
    {
        await using var conn = await GetNewOpenConnection();
        await using var trans = await conn.BeginTransactionAsync();
        
        try
        {
            var round_id = await conn.InsertAsync(round, trans);
            foreach (var score in scores)
            {
                score.round_id = round_id;
                var score_id = await conn.InsertAsync(score, trans);

                var hole_stat = holeStats.FirstOrDefault(x => x.hole_id == score.hole_id);

                if (hole_stat is null) continue; // No hole_stat for this hole.
                
                hole_stat.score_id = score_id;
                hole_stat.round_id = round_id;
                await conn.InsertAsync(hole_stat, trans);
            }

            stats.round_id = round_id;
            await conn.InsertAsync(stats, trans);
            
            await trans.CommitAsync();
            return round_id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occured creating new scorecard.");
            await trans.RollbackAsync();
            return -1;
        }
    }

    public async Task<bool> UpdateScorecardAsync(Round round, List<Score> scores, RoundStats roundStats, List<HoleStats> holeStats)
    {
        await using var conn = await GetNewOpenConnection();
        await using var trans = await conn.BeginTransactionAsync();
        
        try
        {
            // Update round and round stats
            await conn.UpdateAsync(round, trans);
            await conn.UpdateAsync(roundStats, trans);
            
            // Update scores
            foreach (var score in scores)
            {
                await conn.UpdateAsync(score, trans);
            }
            
            // Update hole stats
            foreach (var stat in holeStats)
            {
                await conn.UpdateAsync(stat, trans);
            }
            
            
            await trans.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occured creating new scorecard.");
            await trans.RollbackAsync();
            return false;
        }
    }
    
    public async Task<List<ScorecardSummaryQueryModel>> GetScorecardListByUserIdAsync(string userId, int? limit = null)
    {
        var sql = @"SELECT c.course_name, t.teebox_name, t.slope, t.rating, r.score, r.date_played, r.user_id, r.round_id, t.yardage_out, t.yardage_in, t.yardage_total, r.score_out, r.score_in, t.par
                FROM round as r
	                INNER JOIN course as c ON c.course_id = r.course_id
	                INNER JOIN teebox as t ON t.course_id = c.course_id AND t.teebox_id = r.teebox_id
                WHERE user_id = @userId AND r.is_deleted = false
                ORDER BY date_played DESC";

        if (limit is not null)
        {
            sql += " LIMIT @limit";
        }
            
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<ScorecardSummaryQueryModel>(sql, new {userId, limit});
        return rv.ToList();
    }
    
    public async Task<List<HoleScoreQueryModel>> GetHoleScoresByRoundIdAsync(long roundId)
    {
        var sql = @"SELECT s.hole_score, h.hole_id, h.yardage, h.handicap, h.par, h.hole_number, s.score_id
                    FROM score AS s
                    INNER JOIN hole AS h ON s.hole_id = h.hole_id
                    INNER JOIN teebox AS t ON t.teebox_id = h.teebox_id
                    INNER JOIN round AS r ON r.teebox_id = t.teebox_id 
                        AND r.round_id = s.round_id 
                    WHERE r.round_id = @roundId AND r.is_deleted = false
                    ORDER BY h.hole_number";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<HoleScoreQueryModel>(sql, new {roundId});
        return rv.ToList();
    }
    
    public async Task<List<Score>> GetScoresForRoundByIdAsync(long roundId)
    {
        var sql = "SELECT * FROM score WHERE round_id = @roundId AND is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Score>(sql, new {roundId});
        return rv.ToList();    
    }

    public async Task<RoundStats?> GetRoundStatsByIdAsync(long roundId)
    {
        var sql = "SELECT * FROM round_stats WHERE round_id = @roundId AND is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<RoundStats>(sql, new {roundId});
        return rv;
    }

    public async Task<Round?> GetRoundByIdAsync(long roundId)
    {
        var sql = "SELECT * FROM round WHERE round_id = @roundId AND is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<Round>(sql, new {roundId});
        return rv;
    }

    public async Task<List<HoleStats>> GetHoleStatsByRoundAsync(long roundId)
    {
        var sql = "SELECT * FROM hole_stats WHERE round_id = @roundId AND is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<HoleStats>(sql, new {roundId});
        return rv.ToList();
    }

    public async Task<List<HoleStatsQueryModel>> GetHoleStatsByRoundIdAsync(long roundId)
    {
        var sql = @"SELECT 
                    h.hole_number, 
                    hs.hole_stats_id,
                    hs.round_id,
                    hs.score_id,
                    hs.hole_id,
                    hs.hit_fairway,
	                hs.hit_green,
	                hs.number_of_putts,
	                hs.approach_yardage,
	                hs.miss_fairway_type,
	                hs.miss_green_type,
	                hs.tee_shot_ob,
	                hs.approach_shot_ob,
                    mt_fairway.miss_type AS miss_fairway_type_string, 
                    mt_green.miss_type AS miss_green_type_string
                FROM hole_stats AS hs
                INNER JOIN hole AS h 
                    ON h.hole_id = hs.hole_id
                LEFT JOIN miss_type AS mt_fairway 
                    ON hs.miss_fairway_type = mt_fairway.miss_type_id
                LEFT JOIN miss_type AS mt_green 
                    ON hs.miss_green_type = mt_green.miss_type_id
                WHERE hs.round_id = @roundId
                ORDER BY h.hole_number;";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<HoleStatsQueryModel>(sql, new {roundId});
        return rv.ToList();
    }
}