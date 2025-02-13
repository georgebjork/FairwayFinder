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
    Task<int> CreateNewScorecardAsync(Round round, List<Score> scores, RoundStats stats);
    Task<bool> UpdateScorecardAsync(Round round, List<Score> scores, RoundStats stats);
    Task<List<ScorecardSummaryQueryModel>> GetScorecardSummaryByUserIdAsync(string userId);
    Task<ScorecardSummaryQueryModel?> GetScorecardSummaryByRoundIdAsync(long roundId);
    Task<List<HoleScoreQueryModel>> GetScorecardHoleScoresByRoundIdAsync(long roundId);
    Task<Round?> GetScorecardByIdAsync(long roundId);
    Task<List<Score>> GetScoresForRoundByRoundIdAsync(long roundId);
    Task<RoundStats?> GetRoundStatsForRoundAsync(long roundId);
    Task<Round?> GetRoundByIdAsync(long roundId);
    Task<ScorecardRoundStatsQueryModel?> GetScorecardRoundStatsAsync(long roundId);
}

public class ScorecardRepository(IConfiguration configuration, ILogger<IScorecardRepository> logger) : BasePgRepository(configuration), IScorecardRepository
{
    public async Task<int> CreateNewScorecardAsync(Round round, List<Score> scores, RoundStats stats)
    {
        await using var conn = await GetNewOpenConnection();
        await using var trans = await conn.BeginTransactionAsync();
        
        try
        {
            var round_id = await conn.InsertAsync(round, trans);
            foreach (var score in scores)
            {
                score.round_id = round_id;
                await conn.InsertAsync(score, trans);
            }

            stats.round_id = round_id;
            await conn.InsertAsync(stats, trans);
            
            await trans.CommitAsync();
            return round_id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            await trans.RollbackAsync();
            return -1;
        }
    }

    public async Task<bool> UpdateScorecardAsync(Round round, List<Score> scores, RoundStats stats)
    {
        await using var conn = await GetNewOpenConnection();
        await using var trans = await conn.BeginTransactionAsync();
        
        try
        {
            await conn.UpdateAsync(round, trans);
            foreach (var score in scores)
            {
                await conn.UpdateAsync(score, trans);
            }
            
            // Check if round stats needs to be updated or inserted
            if (stats.round_stats_id > 0) {
                await conn.UpdateAsync(stats, trans);
            }else {
                await conn.InsertAsync(stats, trans);
            }
            
            await trans.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            await trans.RollbackAsync();
            return false;
        }
    }

    public async Task<List<ScorecardSummaryQueryModel>> GetScorecardSummaryByUserIdAsync(string userId)
    {
            var sql = @"SELECT c.course_name, t.teebox_name, t.slope, t.rating, r.score, r.date_played, r.user_id, r.round_id, t.yardage_out, t.yardage_in, t.yardage_total, r.score_out, r.score_in, t.par
                    FROM round as r
	                    INNER JOIN course as c ON c.course_id = r.course_id
	                    INNER JOIN teebox as t ON t.course_id = c.course_id AND t.teebox_id = r.teebox_id
                    WHERE user_id = @userId AND r.is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<ScorecardSummaryQueryModel>(sql, new {userId});
        return rv.ToList();
    }
    
    public async Task<ScorecardSummaryQueryModel?> GetScorecardSummaryByRoundIdAsync(long roundId)
    {
        var sql = @"SELECT c.course_name, t.teebox_name, t.slope, t.rating, r.score, r.date_played, r.user_id, r.round_id, t.yardage_out, t.yardage_in, t.yardage_total, r.score_out, r.score_in, t.par
                    FROM round as r
	                    INNER JOIN course as c ON c.course_id = r.course_id
	                    INNER JOIN teebox as t ON t.course_id = c.course_id AND t.teebox_id = r.teebox_id
                    WHERE r.round_id = @roundId AND r.is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<ScorecardSummaryQueryModel>(sql, new {roundId});
        return rv;
    }

    public async Task<List<HoleScoreQueryModel>> GetScorecardHoleScoresByRoundIdAsync(long roundId)
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

    public async Task<Round?> GetScorecardByIdAsync(long roundId)
    {
        var sql = @"SELECT * FROM round WHERE round_id = @roundId AND is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<Round>(sql, new {roundId});
        return rv;
    }

    public async Task<List<Score>> GetScoresForRoundByRoundIdAsync(long roundId)
    {
        var sql = "SELECT * FROM score WHERE round_id = @roundId AND is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Score>(sql, new {roundId});
        return rv.ToList();    
    }

    public async Task<RoundStats?> GetRoundStatsForRoundAsync(long roundId)
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

    public async Task<ScorecardRoundStatsQueryModel?> GetScorecardRoundStatsAsync(long roundId)
    {
        var sql = "SELECT * FROM round_stats WHERE round_id = @roundId AND is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<ScorecardRoundStatsQueryModel>(sql, new {roundId});
        return rv;
    }
}