using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services;

public interface IStatsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(string userId);
    
    /// <summary>
    /// Gets score trend data for the last N rounds
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="count">Number of rounds to retrieve (default 20)</param>
    /// <returns>List of score trend points ordered by date ascending (oldest first)</returns>
    Task<List<ScoreTrendPointDto>> GetScoreTrendAsync(string userId, int count = 20);
    
    /// <summary>
    /// Gets advanced stats (FIR, GIR, Putting) aggregated across all rounds with hole-by-hole tracking
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Advanced stats DTO with FIR%, GIR%, and average putts</returns>
    Task<AdvancedStatsDto> GetAdvancedStatsAsync(string userId);
    
    /// <summary>
    /// Gets scoring distribution (eagles, birdies, pars, bogeys, etc.) aggregated across all rounds
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Scoring distribution DTO with counts for each category</returns>
    Task<ScoringDistributionDto> GetScoringDistributionAsync(string userId);
    
    /// <summary>
    /// Gets the user's most played courses with round count and average score
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="count">Number of courses to retrieve (default 5)</param>
    /// <returns>List of courses ordered by round count descending</returns>
    Task<List<MostPlayedCourseDto>> GetMostPlayedCoursesAsync(string userId, int count = 5);
    
    /// <summary>
    /// Gets average scoring by par type (Par 3, Par 4, Par 5)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Par type scoring DTO with averages for each par type</returns>
    Task<ParTypeScoringDto> GetParTypeScoringAsync(string userId);
}
