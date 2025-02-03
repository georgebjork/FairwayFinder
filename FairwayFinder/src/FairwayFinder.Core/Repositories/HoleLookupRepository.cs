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
}