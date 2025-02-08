using Dapper;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FairwayFinder.Core.Repositories;

public class HoleLookupRepository(IConfiguration configuration) : BasePgRepository(configuration), IHoleLookupRepository
{
    public async Task<List<Hole>> GetHolesForTeeAsync(long teeboxId)
    {
        var sql = "SELECT * FROM hole WHERE is_deleted = false AND teebox_id = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Hole>(sql, new { Id = teeboxId });
        return rv.ToList();
    }

    public async Task<List<Hole>> GetHolesForRoundByRoundIdAsync(long roundId)
    {
        var sql = @"SELECT * 
                    FROM hole as h
	                    INNER JOIN round as r ON r.teebox_id = h.teebox_id
                    WHERE h.is_deleted = false AND r.round_id = @roundId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Hole>(sql, new { roundId });
        return rv.ToList();
    }
}