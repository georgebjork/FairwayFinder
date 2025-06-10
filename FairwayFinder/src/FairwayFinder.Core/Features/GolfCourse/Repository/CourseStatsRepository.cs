using Dapper;
using FairwayFinder.Core.Features.GolfCourse.Models;
using FairwayFinder.Core.Features.GolfCourse.Models.QueryModels;
using FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;
using FairwayFinder.Core.Features.GolfCourse.Repository.Interfaces;
using FairwayFinder.Core.Repositories;
using Microsoft.Extensions.Configuration;

namespace FairwayFinder.Core.Features.GolfCourse.Repository;

public class CourseStatsRepository(IConfiguration configuration) : BasePgRepository(configuration), ICourseStatsRepository
{
    public async Task<List<CourseStatsRoundQueryModel>> GetRoundsListAsync(CourseStatsRequest request)
    {
        if (request.CourseId <= 0)
        {
            throw new NullReferenceException("CourseId cannot be null");
        }
        
        var sql = @"
            SELECT r.course_id as ""CourseId"", r.teebox_id as ""TeeboxId"", r.score as ""Score"", r.full_round as ""FullRound""
            FROM round as r 
            WHERE r.course_id = @courseId AND r.is_deleted = false AND r.user_id = @userId
        ";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<CourseStatsRoundQueryModel>(sql, new { courseId = request.CourseId, userId = request.UserId });
        return rv.ToList();
    }
    
    public async Task<List<CourseStatsScoresQueryModel>> GetScoresListAsync(CourseStatsRequest request)
    {
        if (request.CourseId <= 0)
        {
            throw new NullReferenceException("CourseId cannot be null");
        }
        
        var sql = @"
            SELECT 
              h.hole_id AS ""HoleId"",
              h.teebox_id AS ""TeeboxId"",
              h.course_id AS ""CourseId"",
              h.par AS ""Par"",
              s.hole_score AS ""HoleScore""
            FROM hole AS h
            INNER JOIN score AS s ON s.hole_id = h.hole_id
            INNER JOIN round as r ON s.round_id = r.round_id
            WHERE h.course_id = @courseId AND r.user_id = @userId AND r.is_deleted = false;
        ";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<CourseStatsScoresQueryModel>(sql, new { courseId = request.CourseId, userId = request.UserId });
        return rv.ToList();
    }

    public async Task<List<CourseStatsHoleStatsResponse>> GetHoleStatsAsync(CourseStatsRequest request)
    {
        if (request.CourseId <= 0)
        {
            throw new NullReferenceException("CourseId cannot be null");
        }
        
        var sql = @"
            SELECT h.hole_number as ""HoleNumber"", ROUND(AVG(s.hole_score), 2) as ""AverageScore"", MIN(s.hole_score) as ""BestScore"", MAX(s.hole_score) as ""WorseScore"", COUNT(s.hole_score) as ""TimesPlayed"", h.par as ""Par"", h.handicap as ""Handicap""
            FROM round as r
              INNER JOIN score as s ON s.round_id = r.round_id
              INNER JOIN hole as h ON h.hole_id = s.hole_id
            WHERE r.course_id = @courseId AND r.is_deleted = false AND r.user_id = @userId
            GROUP BY h.hole_number, h.par, h.handicap
        ";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<CourseStatsHoleStatsResponse>(sql, new { courseId = request.CourseId, userId = request.UserId });
        return rv.ToList();
    }
}