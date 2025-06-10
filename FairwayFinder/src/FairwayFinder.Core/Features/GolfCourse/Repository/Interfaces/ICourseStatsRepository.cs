using FairwayFinder.Core.Features.GolfCourse.Models;
using FairwayFinder.Core.Features.GolfCourse.Models.QueryModels;
using FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;
using FairwayFinder.Core.Repositories.Interfaces;

namespace FairwayFinder.Core.Features.GolfCourse.Repository.Interfaces;

public interface ICourseStatsRepository : IBaseRepository
{
    Task<List<CourseStatsRoundQueryModel>> GetRoundsListAsync(CourseStatsRequest request);
    Task<List<CourseStatsScoresQueryModel>> GetScoresListAsync(CourseStatsRequest request);
    Task<List<CourseStatsHoleStatsResponse>> GetHoleStatsAsync(CourseStatsRequest request);
}