using Dapper;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FairwayFinder.Core.Repositories;

public class TeeboxLookupRepository(IConfiguration configuration) : BasePgRepository(configuration), ITeeboxLookupRepository
{
    public async Task<Teebox?> GetTeeByIdAsync(long teeboxId)
    {
        var sql = "SELECT * FROM teebox WHERE is_deleted = false AND teebox_id = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<Teebox>(sql, new { Id = teeboxId });
        return rv;
    }

    public async Task<List<Teebox>> GetTeesForCourseAsync(long courseId)
    {
        var sql = "SELECT * FROM teebox WHERE is_deleted = false AND course_id = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Teebox>(sql, new { Id = courseId });
        return rv.ToList();
    }

    public async Task<Dictionary<string, string>> GetTeesDropdownForCourseAsync(long courseId)
    {
        var sql = "SELECT teebox_id::text as Key, teebox_name as Value FROM teebox WHERE is_deleted = false AND course_id = @courseId ORDER BY yardage_total";
        await using var conn = await GetNewOpenConnection();
        var data = await conn.QueryAsync<KeyValuePair<string, string>>(sql, new {courseId});
        return new Dictionary<string, string>(data);
    }
}