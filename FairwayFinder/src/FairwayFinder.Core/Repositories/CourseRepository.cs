using Dapper;
using FairwayFinder.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Repositories;

public interface ICourseRepository : IBaseRepository
{
    Task<List<Course>> GetAllCourses();
    Task<Course?> GetCourseById(int courseId);
}

public class CourseRepository(IConfiguration configuration, ILogger<CourseRepository> logger) : BasePgRepository(configuration, logger), ICourseRepository 
{
    public async Task<List<Course>> GetAllCourses()
    {
        var sql = "SELECT * FROM public.course WHERE is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Course>(sql);
        return rv.ToList();
    }

    public async Task<Course?> GetCourseById(int courseId)
    {
        var sql = "SELECT * FROM public.course WHERE course_id = @courseId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<Course>(sql, new {courseId = courseId});
        return rv;
    }
}