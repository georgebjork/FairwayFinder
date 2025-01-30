using Dapper;
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
}