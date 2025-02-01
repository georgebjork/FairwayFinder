using Dapper;
using Dapper.Contrib.Extensions;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Repositories;

public interface ICourseManagementRepository : IBaseRepository
{
    public Task<List<Course>> GetAllAsync();
    public Task<Course?> GetCourseByIdAsync(long courseId);
    public Task<Teebox?> GetTeeByIdAsync(long teeboxId);
    public Task<List<Teebox>> GetTeeForCourseAsync(long courseId);
    public Task<List<Hole>> GetHolesForTeeAsync(long teeboxId);
    
    // Inserts
    public Task<int> InsertNewTeeAsync(Teebox teebox, List<Hole> holes);
    public Task<bool> UpdateTeeAsync(Teebox tee, List<Hole> holes);
}

public class CourseManagementRepository(IConfiguration configuration, ILogger<ICourseManagementRepository> logger) : BasePgRepository(configuration), ICourseManagementRepository 
{
    public async Task<List<Course>> GetAllAsync()
    {
        var sql = "SELECT * FROM course WHERE is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Course>(sql);
        return rv.ToList();
    }

    public async Task<Course?> GetCourseByIdAsync(long courseId)
    {
        var sql = "SELECT * FROM course WHERE is_deleted = false AND course_id = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<Course>(sql, new { Id = courseId });
        return rv;
    }

    public async Task<Teebox?> GetTeeByIdAsync(long teeboxId)
    {
        var sql = "SELECT * FROM teebox WHERE is_deleted = false AND teebox_id = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<Teebox>(sql, new { Id = teeboxId });
        return rv;
    }

    public async Task<List<Teebox>> GetTeeForCourseAsync(long courseId)
    {
        var sql = "SELECT * FROM teebox WHERE is_deleted = false AND course_id = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Teebox>(sql, new { Id = courseId });
        return rv.ToList();
    }

    public async Task<List<Hole>> GetHolesForTeeAsync(long teeboxId)
    {
        var sql = "SELECT * FROM hole WHERE is_deleted = false AND teebox_id = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Hole>(sql, new { Id = teeboxId });
        return rv.ToList();
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