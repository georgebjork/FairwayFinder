using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IStatsService
{
    /// <summary>
    /// Gets all stats for a user aggregated across their rounds.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="filter">Optional filter for round type and date range</param>
    /// <param name="coursesCount">Number of most played courses to return (default 5)</param>
    /// <returns>Complete user stats</returns>
    Task<UserStatsResponse> GetUserStatsAsync(string userId, StatsFilter? filter = null, int coursesCount = 5);
    
    /// <summary>
    /// Gets course-specific stats for a user at a particular course.
    /// Includes per-hole aggregate stats, overall averages, scoring distribution, and round history.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="courseId">Course ID</param>
    /// <param name="teeboxId">Optional teebox filter. Null = all teeboxes.</param>
    /// <param name="startDate">Optional start date (inclusive). Null = no lower bound.</param>
    /// <param name="endDate">Optional end date (inclusive). Null = no upper bound.</param>
    /// <returns>Course-specific stats, or null if user has no rounds at this course</returns>
    Task<CourseStatsResponse?> GetCourseStatsAsync(string userId, long courseId, long? teeboxId = null, DateOnly? startDate = null, DateOnly? endDate = null);
    
    /// <summary>
    /// Gets the distinct years that a user has played rounds in.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of years in descending order</returns>
    Task<List<int>> GetAvailableYearsAsync(string userId);
    
    /// <summary>
    /// Gets the distinct courses a user has played, for populating a filter dropdown.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of course options sorted by name</returns>
    Task<List<CourseOption>> GetUserCoursesAsync(string userId);
}
