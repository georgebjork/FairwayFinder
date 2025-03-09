using Dapper;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Stats.Models.QueryModels;
using Microsoft.Extensions.Configuration;

namespace FairwayFinder.Core.Stats.Repositories;

public interface ICourseStatsRepository : IBaseRepository
{
    public Task<List<CourseStatsQueryModel>> GetOverallCourseStatsAsync(CourseStatsRequest request);
    public Task GetHoleScoreCourseStatsAsync();
}

public class CourseStatsRepository(IConfiguration configuration) : BasePgRepository(configuration), ICourseStatsRepository
{
    public async Task<List<CourseStatsQueryModel>> GetOverallCourseStatsAsync(CourseStatsRequest request)
    {
        var sql = @"
            SELECT t.teebox_id, t.teebox_name, COUNT(r.*) as number_of_rounds, AVG(r.score) as avgerage_score, MIN(r.score) as low_score, MAX(r.score) as high_score
            FROM round as r 
	            INNER JOIN teebox as t ON t.teebox_id = r.teebox_id
            WHERE r.course_id = @courseId
            GROUP BY t.teebox_id
        ";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<CourseStatsQueryModel>(sql, new { courseId = request.CourseId });
        return rv.ToList();
    }

    public Task GetHoleScoreCourseStatsAsync()
    {
        throw new NotImplementedException();
    }
}