using FairwayFinder.Core.Features.GolfCourse.Models;
using FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;

namespace FairwayFinder.Core.Features.GolfCourse.Services.Interfaces;

public interface ICourseStatsService
{
    Task<CourseStatsResponse> GetAllCourseStatsAsync(CourseStatsRequest request);
    Task<CourseStatsScoreCountsResponse> GetScoreCountsAsync(CourseStatsRequest request);
    Task<CourseStatsRoundsResponse> GetRoundScoresAsync(CourseStatsRequest request);
    Task<List<CourseStatsHoleStatsResponse>> GetHoleStatsAsync(CourseStatsRequest request);

}