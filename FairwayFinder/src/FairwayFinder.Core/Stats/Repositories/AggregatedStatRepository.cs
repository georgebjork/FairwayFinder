using Dapper;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Stats.Models.QueryModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Stats.Repositories;

public interface IAggregatedStatRepository : IBaseRepository
{
    public Task<RoundScoreStatsQueryModel> GetScoreStatsByUserIdAsync(string userId);
    public Task<List<RoundsQueryModel>> GetRoundsByUserId(string userId, StatsRequest request);
    public Task<long> GetNumberOfRoundsPlayedAsync(string userId, StatsRequest request);
    public Task<double> GetAverageScoreOfRoundsAsync(string userId, StatsRequest request);
    public Task<int> GetLowScoreOfRoundsAsync(string userId, StatsRequest request);
}

public class AggregatedStatRepository(IConfiguration configuration, ILogger<AggregatedStatRepository> logger) : BasePgRepository(configuration), IAggregatedStatRepository
{
    private readonly ILogger<AggregatedStatRepository> _logger = logger;
    
    public async Task<RoundScoreStatsQueryModel> GetScoreStatsByUserIdAsync(string userId)
    {
        var sql = @"SELECT SUM(hole_in_one) as hole_in_one, SUM(double_eagles) as double_eagle, SUM(eagles) as eagles, SUM(birdies) as birdies, SUM(pars) as pars, SUM(bogies) as bogies, SUM(double_bogies) as double_bogies, SUM(triple_or_worse) as triple_or_worse
                    FROM round_stats as rs
	                    INNER JOIN round as r ON r.round_id = rs.round_id
                    WHERE r.user_id = @userId AND r.exclude_from_stats = False";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<RoundScoreStatsQueryModel>(sql, new { userId });
        return rv ?? new RoundScoreStatsQueryModel();
    }

    public async Task<List<RoundsQueryModel>> GetRoundsByUserId(string userId, StatsRequest request)
    {
        var sql = @"SELECT r.*, t.teebox_name, t.slope, t.rating, c.course_name, r.full_round, r.front_nine, r.back_nine
            FROM round as r
                INNER JOIN course as c ON c.course_id = r.course_id
                INNER JOIN teebox as t ON t.teebox_id = r.teebox_id 
            WHERE r.is_deleted = False AND r.user_id = @userId AND r.exclude_from_stats = False";
        
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
        var sql = "SELECT count(*) as round_count FROM round WHERE is_deleted = False AND user_id = @userId AND exclude_from_stats = False";

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
        var sql = @"SELECT avg(score) as round_avg 
                    FROM round 
                    WHERE is_deleted = False 
                      AND user_id = @userId 
                      AND exclude_from_stats = False
                      AND full_round = @fullRound";
        
        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }

        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<double>(sql, new { userId, year = request.Year, fullRound = !request.NineHole });
        return rv;
    }

    public async Task<int> GetLowScoreOfRoundsAsync(string userId, StatsRequest request)
    {
        var sql = @"SELECT min(score) as low_score 
                    FROM round 
                    WHERE is_deleted = False 
                        AND user_id = @userId
                        AND exclude_from_stats = False
                        AND full_round = @fullRound";
        
        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }
        
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<int>(sql, new { userId, year = request.Year, fullRound = !request.NineHole });
        return rv;
    }
}