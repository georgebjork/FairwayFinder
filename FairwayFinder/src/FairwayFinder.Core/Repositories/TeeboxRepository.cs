using Dapper;
using Dapper.Contrib.Extensions;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FairwayFinder.Core.Repositories;

public class TeeboxRepository(IConfiguration configuration) : BasePgRepository(configuration), ITeeboxRepository
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
    
    public async Task<int> InsertNewTeeAsync(Teebox teebox, List<Hole> holes)
    {
        await using var conn = await GetNewOpenConnection();
        await using var trans = await conn.BeginTransactionAsync();

        try
        {
            var teeId = await conn.InsertAsync(teebox, trans);

            foreach (var item in holes)
            {
                item.teebox_id = teeId;
                item.course_id = teebox.course_id;
                await conn.InsertAsync(item, trans);
            }
            
            await trans.CommitAsync();
            return teeId;
        }
        catch (Exception ex)
        {
            await trans.RollbackAsync();
            return -1;
        }
    }
    
    public async Task<bool> UpdateTeeAsync(Teebox tee, List<Hole> holes)
    {
        await using var conn = await GetNewOpenConnection();
        await using var trans = await conn.BeginTransactionAsync();
        
        try
        {
            await conn.UpdateAsync(tee, trans);
            foreach (var item in holes)
            {
                await conn.UpdateAsync(item, trans);
            }
            
            await trans.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await trans.RollbackAsync();
            return false;
        }
    }
}