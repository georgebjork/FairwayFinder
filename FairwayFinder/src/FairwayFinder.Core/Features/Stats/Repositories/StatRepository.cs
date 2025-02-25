using Dapper;
using FairwayFinder.Core.Features.Stats.Models.QueryModels;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Stats.Repositories;

public interface IStatRepository : IBaseRepository
{
    public Task<RoundScoreStatsQueryModel> GetScoreStatsByUserIdAsync(string userId);
    public Task<RoundScoreStatsQueryModel> GetScoreStatsByRoundIdAsync(long roundId);
    public Task<List<RoundScoreQueryModel>> GetRoundScoresByUserId(string userId);
    public Task<long> GetNumberOfRoundsPlayedAsync(StatsRequest request);
    public Task<double> GetAverageScoreOfRoundsAsync(StatsRequest request);
    public Task<int> GetLowScoreOfRoundsAsync(StatsRequest request);
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

    public async Task<List<RoundScoreQueryModel>> GetRoundScoresByUserId(string userId)
    {
        var sql = "SELECT score, date_played FROM round WHERE is_deleted = False AND user_id = @userId ORDER BY date_played";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<RoundScoreQueryModel>(sql, new { userId });
        return rv.ToList();
    }

    public async Task<long> GetNumberOfRoundsPlayedAsync(StatsRequest request)
    {
        var sql = "SELECT count(*) as round_count FROM round WHERE is_deleted = False AND user_id = @userId";

        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }
        
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<long>(sql, new { userId = request.UserId, year = request.Year });
        return rv;
    }

    public async Task<double> GetAverageScoreOfRoundsAsync(StatsRequest request)
    {
        var sql = "SELECT avg(score) as round_avg FROM round WHERE is_deleted = False AND user_id = @userId";
        
        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }

        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<double>(sql, new { userId = request.UserId, year = request.Year });
        return rv;
    }

    public async Task<int> GetLowScoreOfRoundsAsync(StatsRequest request)
    {
        var sql = "SELECT min(score) as low_score FROM round WHERE is_deleted = False AND user_id = @userId";
        
        if (request.Year is not null)
        {
            sql += " AND EXTRACT(YEAR FROM date_played) = @year";
        }
        
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<int>(sql, new { userId = request.UserId, year = request.Year });
        return rv;
    }
}