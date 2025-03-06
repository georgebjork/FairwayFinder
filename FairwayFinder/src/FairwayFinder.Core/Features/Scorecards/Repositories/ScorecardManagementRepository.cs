using Dapper.Contrib.Extensions;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Scorecards.Repositories;

public interface IScorecardManagementRepository : IBaseRepository
{
    public Task<int> CreateNewScorecardAsync(Round round, List<Score> scores, RoundStats stats, List<HoleStats> holeStats);
    public Task<bool> UpdateScorecardAsync(Round round, List<Score> scores, RoundStats roundStats, List<HoleStats> holeStats);
}

public class ScorecardManagementRepository(IConfiguration configuration, ILogger<IScorecardManagementRepository> logger) : BasePgRepository(configuration), IScorecardManagementRepository
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
}