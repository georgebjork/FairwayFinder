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
    Task<int> CreateNewScorecardAsync(Round round, List<Score> scores);
    Task<List<ScorecardSummaryQueryModel>> GetScorecardSummaryByUserIdAsync(string userId);
}

public class ScorecardRepository(IConfiguration configuration, ILogger<IScorecardRepository> logger) : BasePgRepository(configuration), IScorecardRepository
{
    public async Task<int> CreateNewScorecardAsync(Round round, List<Score> scores)
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

    public async Task<List<ScorecardSummaryQueryModel>> GetScorecardSummaryByUserIdAsync(string userId)
    {
        var sql = @"SELECT c.course_name, t.teebox_name, t.slope, t.rating, r.score, r.date_played, r.user_id
                    FROM round as r
	                    INNER JOIN course as c ON c.course_id = r.course_id
	                    INNER JOIN teebox as t ON t.course_id = c.course_id 
                    WHERE user_id = @userId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<ScorecardSummaryQueryModel>(sql, new {userId});
        return rv.ToList();
    }
}