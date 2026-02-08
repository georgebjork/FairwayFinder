using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services;

public interface IStatsService
{
    /// <summary>
    /// Gets all stats for a user aggregated across their rounds.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="trendCount">Number of rounds for score trend (default 20)</param>
    /// <param name="coursesCount">Number of most played courses to return (default 5)</param>
    /// <returns>Complete user stats</returns>
    Task<UserStatsResponse> GetUserStatsAsync(string userId, int trendCount = 20, int coursesCount = 5);
}
