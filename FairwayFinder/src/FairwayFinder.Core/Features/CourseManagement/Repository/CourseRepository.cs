using Dapper;
using FairwayFinder.Core.Features.CourseManagement.Models.QueryModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Repository;

public interface ICourseRepository : IBaseRepository
{
    Task<List<GetAllCoursesQueryModel>> GetAllCourses();
    Task<Course?> GetCourseById(int courseId);
}

public class CourseRepository(IConfiguration configuration, ILogger<CourseRepository> logger) : BasePgRepository(configuration, logger), ICourseRepository 
{
    public async Task<List<GetAllCoursesQueryModel>> GetAllCourses()
    {
        var sql = "SELECT course_id, course_name FROM public.course";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<GetAllCoursesQueryModel>(sql);
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