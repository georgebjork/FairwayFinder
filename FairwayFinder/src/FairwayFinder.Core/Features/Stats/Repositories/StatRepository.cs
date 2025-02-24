using Dapper;
using FairwayFinder.Core.Features.Stats.Models.QueryModels;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Stats.Repositories;

public interface IStatRepository : IBaseRepository
{
    public Task<RoundScoreStats> GetScoreStatsByUserIdAsync(string userId);
    public Task<RoundScoreStats> GetScoreStatsByRoundIdAsync(long roundId);

    public Task<long> GetNumberOfRoundsPlayedAsync(string userId);
    public Task<double> GetAverageScoreOfRoundsAsync(string userId);
    public Task<int> GetLowScoreOfRoundsAsync(string userId);
}

public class StatRepository(IConfiguration configuration, ILogger<StatRepository> logger) : BasePgRepository(configuration), IStatRepository
{
    private readonly ILogger<StatRepository> _logger = logger;
    
    public async Task<RoundScoreStats> GetScoreStatsByUserIdAsync(string userId)
    {
        var sql = @"SELECT SUM(hole_in_one) as hole_in_one, SUM(double_eagles) as double_eagle, SUM(eagles) as eagles, SUM(birdies) as birdies, SUM(pars) as pars, SUM(bogies) as bogies, SUM(double_bogies) as double_bogies, SUM(triple_or_worse) as triple_or_worse
                    FROM round_stats as rs
	                    INNER JOIN round as r ON r.round_id = rs.round_id
                    WHERE r.user_id = @userId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<RoundScoreStats>(sql, new { userId });
        return rv ?? new RoundScoreStats();
    }

    public async Task<RoundScoreStats> GetScoreStatsByRoundIdAsync(long roundId)
    {
        var sql = @"SELECT SUM(hole_in_one) as hole_in_one, SUM(double_eagles) as double_eagle, SUM(eagles) as eagles, SUM(birdies) as birdies, SUM(pars) as pars, SUM(bogies) as bogies, SUM(double_bogies) as double_bogies, SUM(triple_or_worse) as triple_or_worse
                    FROM round_stats as rs
                    WHERE rs.round_id = @roundId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<RoundScoreStats>(sql, new { roundId });
        return rv ?? new RoundScoreStats();
    }

    public async Task<long> GetNumberOfRoundsPlayedAsync(string userId)
    {
        var sql = "SELECT count(*) as round_count FROM round WHERE is_deleted = False AND user_id = @userId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<long>(sql, new { userId });
        return rv;
    }

    public async Task<double> GetAverageScoreOfRoundsAsync(string userId)
    {
        var sql = "SELECT avg(score) as round_avg FROM round WHERE is_deleted = False AND user_id = @userId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<double>(sql, new { userId });
        return rv;
    }

    public async Task<int> GetLowScoreOfRoundsAsync(string userId)
    {
        var sql = "SELECT min(score) as low_score FROM round WHERE is_deleted = False AND user_id = @userId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteScalarAsync<int>(sql, new { userId });
        return rv;
    }
}