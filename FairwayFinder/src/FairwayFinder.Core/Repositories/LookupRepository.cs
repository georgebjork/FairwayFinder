using Dapper;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Repositories;

public interface ILookupRepository : IBaseRepository
{
    public Task<Dictionary<int, string>> GetMissTypes();
    public Task<Dictionary<long, string>> GetTeesForCourseAsync(long courseId);
    public Task<Dictionary<long, string>> GetDistinctYearsFromRoundsAsync(string userId);
}

public class LookupRepository(IConfiguration configuration, ILogger<LookupRepository> logger) : BasePgRepository(configuration), ILookupRepository
{
    private readonly ILogger<LookupRepository> _logger = logger;

    public async Task<Dictionary<int, string>> GetMissTypes()
    {
        var sql = "SELECT miss_type_id as Key, miss_type as Value FROM miss_type";
        await using var conn = await GetNewOpenConnection();
        var data = await conn.QueryAsync<KeyValuePair<int, string>>(sql);
        return new Dictionary<int, string>(data);
    }

    public async Task<Dictionary<long, string>> GetTeesForCourseAsync(long courseId)
    {
        var sql = "SELECT teebox_id::text as Key, teebox_name as Value FROM teebox WHERE is_deleted = false AND course_id = @courseId ORDER BY yardage_total";
        await using var conn = await GetNewOpenConnection();
        var data = await conn.QueryAsync<KeyValuePair<long, string>>(sql, new {courseId});
        return new Dictionary<long, string>(data);
    }
    
    public async Task<Dictionary<long, string>> GetDistinctYearsFromRoundsAsync(string userId)
    {
        var sql = "SELECT DISTINCT EXTRACT(YEAR FROM date_played) as Key, EXTRACT(YEAR FROM date_played) as Value FROM round WHERE user_id = @userId ORDER BY Key;";
        await using var conn = await GetNewOpenConnection();
        var data = await conn.QueryAsync<KeyValuePair<long, string>>(sql, new {userId});
        return new Dictionary<long, string>(data);
    }
}