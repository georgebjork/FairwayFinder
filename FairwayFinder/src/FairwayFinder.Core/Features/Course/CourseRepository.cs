using Dapper;
using FairwayFinder.Core.Repositories;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Course;

public interface ICourseRepository : IBaseRepository
{
    Task<Models.Course?> GetCourse(int courseId);
}

public class CourseRepository(IConfiguration configuration, ILogger<CourseRepository> logger) : BasePgRepository(configuration, logger), ICourseRepository
{
    
    public async Task<Models.Course?> GetCourse(int courseId)
    {
        const string sql = "SELECT * FROM public.course WHERE course_id = @courseId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<Models.Course>(sql, new { courseId });
        return rv;
    }
}