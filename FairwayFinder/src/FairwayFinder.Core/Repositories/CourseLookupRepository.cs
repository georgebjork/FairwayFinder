using Dapper;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Repositories;

public class CourseLookupRepository(IConfiguration configuration, ILogger<CourseLookupRepository> repository) : BasePgRepository(configuration), ICourseLookupRepository
{
    
    public async Task<List<Course>> GetAllCoursesAsync()
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
    
    public async Task<List<Course>> CourseSearchByName(string name)
    {
        var sql = "SELECT * FROM course WHERE is_deleted = false AND course_name LIKE @name";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Course>(sql, new { name = $"%{name}%" });        
        return rv.ToList();
    }
}