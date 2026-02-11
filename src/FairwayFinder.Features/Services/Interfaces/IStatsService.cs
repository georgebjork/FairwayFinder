using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IStatsService
{
    /// <summary>
    /// Gets all stats for a user aggregated across their rounds.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="filter">Optional filter for round type and date range</param>
    /// <param name="trendCount">Number of rounds for score trend (default 20)</param>
    /// <param name="coursesCount">Number of most played courses to return (default 5)</param>
    /// <returns>Complete user stats</returns>
    Task<UserStatsResponse> GetUserStatsAsync(string userId, StatsFilter? filter = null, int trendCount = 20, int coursesCount = 5);
    
    /// <summary>
    /// Gets the distinct years that a user has played rounds in.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of years in descending order</returns>
    Task<List<int>> GetAvailableYearsAsync(string userId);
}
