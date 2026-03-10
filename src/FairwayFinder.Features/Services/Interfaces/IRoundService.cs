using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IRoundService
{
    /// <summary>
    /// Gets a lightweight list of rounds for a user (for display in lists)
    /// </summary>
    Task<List<RoundResponse>> GetRoundsByUserIdAsync(string userId);
    
    /// <summary>
    /// Gets fully loaded rounds with all related data (holes, scores, stats)
    /// </summary>
    Task<List<RoundResponse>> GetRoundsWithDetailsAsync(string userId);
    
    /// <summary>
    /// Gets a single round with all related data (holes, scores, stats)
    /// </summary>
    Task<RoundResponse?> GetRoundByIdAsync(long roundId);

    /// <summary>
    /// Gets a list of all courses that a user has played
    /// </summary>
    Task<List<CourseResponse>> GetPlayedCoursesByUserId(string userId, bool? statRounds = null);

    /// <summary>
    /// Checks if a round belongs to a specific user.
    /// </summary>
    Task<bool> IsRoundOwnedByUserAsync(long roundId, string userId);

    /// <summary>
    /// Creates a new round with scores, hole stats (if enabled), and round stats.
    /// Returns the new round ID.
    /// </summary>
    Task<long> CreateRoundAsync(CreateRoundRequest request);

    /// <summary>
    /// Updates an existing round's teebox, date, scores, hole stats, and round stats.
    /// Returns false if the round was not found or the user does not own it.
    /// </summary>
    Task<bool> UpdateRoundAsync(UpdateRoundRequest request);

    /// <summary>
    /// Soft-deletes a round and all its child records (scores, hole stats, round stats).
    /// Returns false if the round was not found.
    /// </summary>
    Task<bool> DeleteRoundAsync(long roundId, string userId);
}
