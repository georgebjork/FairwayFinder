using Dapper;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Stats.Models.QueryModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Stats.Repositories;

public interface IScorecardStatRepository : IBaseRepository
{
	public Task<RoundScoreStatsQueryModel> GetScoreStatsByRoundIdAsync(long roundId);
	public Task<List<AverageScoreByParQueryModel>> GetAverageScoresByParAsync(string userId, StatsRequest request);

}

public class ScorecardStatRepository(IConfiguration configuration, ILogger<IScorecardStatRepository> logger) : BasePgRepository(configuration), IScorecardStatRepository
{
    private readonly ILogger<IScorecardStatRepository> _logger = logger;
    
    
    public async Task<RoundScoreStatsQueryModel> GetScoreStatsByRoundIdAsync(long roundId)
    {
        var sql = @"SELECT SUM(hole_in_one) as hole_in_one, SUM(double_eagles) as double_eagle, SUM(eagles) as eagles, SUM(birdies) as birdies, SUM(pars) as pars, SUM(bogies) as bogies, SUM(double_bogies) as double_bogies, SUM(triple_or_worse) as triple_or_worse
                    FROM round_stats as rs
                    WHERE rs.round_id = @roundId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<RoundScoreStatsQueryModel>(sql, new { roundId });
        return rv ?? new RoundScoreStatsQueryModel();
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