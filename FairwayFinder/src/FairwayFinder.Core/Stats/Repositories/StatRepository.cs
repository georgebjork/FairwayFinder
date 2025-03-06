using Dapper;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Stats.Models.QueryModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Stats.Repositories;

public interface IStatRepository : IBaseRepository
{
    public Task<RoundScoreStatsQueryModel> GetScoreStatsByUserIdAsync(string userId);
    public Task<RoundScoreStatsQueryModel> GetScoreStatsByRoundIdAsync(long roundId);
    public Task<List<RoundsQueryModel>> GetRoundsByUserId(string userId, StatsRequest request);
    public Task<long> GetNumberOfRoundsPlayedAsync(string userId, StatsRequest request);
    public Task<double> GetAverageScoreOfRoundsAsync(string userId, StatsRequest request);
    public Task<int> GetLowScoreOfRoundsAsync(string userId, StatsRequest request);
    public Task<List<AverageScoreByParQueryModel>> GetAverageScoresByParAsync(string userId, StatsRequest request);
}

public class StatRepository(IConfiguration configuration, ILogger<StatRepository> logger) : BasePgRepository(configuration), IStatRepository
{
    private readonly ILogger<StatRepository> _logger = logger;
    
    public async Task<RoundScoreStatsQueryModel> GetScoreStatsByUserIdAsync(string userId)
    {
        var sql = @"SELECT SUM(hole_in_one) as hole_in_one, SUM(double_eagles) as double_eagle, SUM(eagles) as eagles, SUM(birdies) as birdies, SUM(pars) as pars, SUM(bogies) as bogies, SUM(double_bogies) as double_bogies, SUM(triple_or_worse) as triple_or_worse
                    FROM round_stats as rs
	                    INNER JOIN round as r ON r.round_id = rs.round_id
                    WHERE r.user_id = @userId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<RoundScoreStatsQueryModel>(sql, new { userId });
        return rv ?? new RoundScoreStatsQueryModel();
    }

    public async Task<RoundScoreStatsQueryModel> GetScoreStatsByRoundIdAsync(long roundId)
    {
        var sql = @"SELECT SUM(hole_in_one) as hole_in_one, SUM(double_eagles) as double_eagle, SUM(eagles) as eagles, SUM(birdies) as birdies, SUM(pars) as pars, SUM(bogies) as bogies, SUM(double_bogies) as double_bogies, SUM(triple_or_worse) as triple_or_worse
                    FROM round_stats as rs
                    WHERE rs.round_id = @roundId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<RoundScoreStatsQueryModel>(sql, new { roundId });
        return rv ?? new RoundScoreStatsQueryModel();
    }

    public async Task<List<RoundsQueryModel>> GetRoundsByUserId(string userId, StatsRequest request)
    {
        var sql = @"SELECT r.*, t.teebox_name, t.slope, t.rating, c.course_name
            FROM round as r
                INNER JOIN course as c ON c.course_id = r.course_id
                INNER JOIN teebox as t ON t.teebox_id = r.teebox_id 
            WHERE r.is_deleted = False AND r.user_id = @userId";
        
        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM r.date_played) = @year";
        }

        sql += " ORDER BY r.date_played";
        
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<RoundsQueryModel>(sql, new { userId, year = request.Year });
        return rv.ToList();
    }

    public async Task<long> GetNumberOfRoundsPlayedAsync(string userId, StatsRequest request)
    {
        var sql = "SELECT count(*) as round_count FROM round WHERE is_deleted = False AND user_id = @userId";

        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }
        
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<long>(sql, new { userId, year = request.Year });
        return rv;
    }

    public async Task<double> GetAverageScoreOfRoundsAsync(string userId, StatsRequest request)
    {
        var sql = "SELECT avg(score) as round_avg FROM round WHERE is_deleted = False AND user_id = @userId";
        
        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }

        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<double>(sql, new { userId, year = request.Year });
        return rv;
    }

    public async Task<int> GetLowScoreOfRoundsAsync(string userId, StatsRequest request)
    {
        var sql = "SELECT min(score) as low_score FROM round WHERE is_deleted = False AND user_id = @userId";
        
        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }
        
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<int>(sql, new { userId, year = request.Year });
        return rv;
    }

    public async Task<List<AverageScoreByParQueryModel>> GetAverageScoresByParAsync(string userId, StatsRequest request)
    {
        var sql = @"SELECT 
	                h.par,
                    AVG(CASE WHEN hs.hit_fairway = true THEN s.hole_score END) AS average_score_fairway_hit,
                    COUNT(CASE WHEN hs.hit_fairway = true THEN s.hole_score END) AS average_score_fairway_hit_count,

	                AVG(CASE WHEN hs.hit_fairway = false THEN s.hole_score END) AS average_score_fairway_miss,
	                COUNT(CASE WHEN hs.hit_fairway = false THEN s.hole_score END) AS average_score_fairway_miss_count,
	                
	                AVG(CASE WHEN hs.hit_green = true THEN s.hole_score END) AS average_score_green_hit,
	                COUNT(CASE WHEN hs.hit_green = true THEN s.hole_score END) AS average_score_green_hit_count,

	                AVG(CASE WHEN hs.hit_green = false THEN s.hole_score END) AS average_score_green_miss,
	                COUNT(CASE WHEN hs.hit_green = false THEN s.hole_score END) AS average_score_green_miss_count,
	                
	                AVG(CASE WHEN hs.hit_green = true AND hs.hit_fairway = true THEN s.hole_score END) AS average_score_both_hit,
	                COUNT(CASE WHEN hs.hit_green = true AND hs.hit_fairway = true THEN s.hole_score END) AS average_score_both_hit_count,

	                AVG(CASE WHEN hs.hit_green = false AND hs.hit_fairway = false THEN s.hole_score END) AS average_score_both_miss,
	                COUNT(CASE WHEN hs.hit_green = false AND hs.hit_fairway = false THEN s.hole_score END) AS average_score_both_miss_count,
	    
	                AVG(s.hole_score) AS average_score,
	                COUNT(s.hole_score) AS average_score_count

                FROM score as s
                INNER JOIN hole as h ON s.hole_id = h.hole_id
                LEFT JOIN hole_stats AS hs ON s.score_id = hs.score_id
                WHERE 1=1";
        
        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }
        
        if (request.RoundId is not null && request.RoundId > 0)
        {
            sql += " AND s.round_id = @roundId";
        }

        sql += "  GROUP BY par";
        
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<AverageScoreByParQueryModel>(sql, new { year = request.Year, roundId = request.RoundId });
        return rv.ToList();
    }
}