using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IRoundService
{
    /// <summary>
    /// Gets a lightweight list of rounds for a user (for display in lists)
    /// </summary>
    Task<List<RoundResponse>> GetRoundsByUserIdAsync(string userId);

    /// <summary>
    /// Lightweight list of rounds for a user with stats-style filtering applied in the database
    /// (round type, date range, course). Rounds marked ExcludeFromStats are omitted so the list
    /// matches what stats endpoints aggregate over.
    /// </summary>
    Task<List<RoundResponse>> GetRoundsByUserIdAsync(string userId, StatsFilter? filter);

    /// <summary>
    /// Gets fully loaded rounds with all related data (holes, scores, stats)
    /// </summary>
    Task<List<RoundResponse>> GetRoundsWithDetailsAsync(string userId);

    /// <summary>
    /// Fully loaded rounds with all related data, filtered at the database level by round type,
    /// date range, and/or course. Child collections (scores, holes, hole stats) are loaded only
    /// for the filtered round set. Rounds marked ExcludeFromStats are omitted.
    /// </summary>
    Task<List<RoundResponse>> GetRoundsWithDetailsAsync(string userId, StatsFilter? filter);

    /// <summary>
    /// Fully loaded rounds as above, with strokes gained computed relative to the given golfer
    /// <paramref name="level"/> (Tour = raw benchmark; handicap levels apply a per-category offset).
    /// </summary>
    Task<List<RoundResponse>> GetRoundsWithDetailsAsync(string userId, StatsFilter? filter, BaselineLevel level);

    /// <summary>
    /// Gets a single round with all related data (holes, scores, stats)
    /// </summary>
    Task<RoundResponse?> GetRoundByIdAsync(long roundId);

    /// <summary>
    /// Gets a single round as above, with strokes gained computed relative to the given golfer level.
    /// </summary>
    Task<RoundResponse?> GetRoundByIdAsync(long roundId, BaselineLevel level);

    /// <summary>
    /// Gets a list of all courses that a user has played
    /// </summary>
    Task<List<CourseResponse>> GetPlayedCoursesByUserId(string userId, bool? statRounds = null);

    /// <summary>
    /// Checks if a round belongs to a specific user.
    /// </summary>
    Task<bool> IsRoundOwnedByUserAsync(long roundId, string userId);

    /// <summary>
    /// Returns the owning user's id for a round, or null if the round does not exist (or is deleted).
    /// A single lightweight projection query that lets callers distinguish "not found" (null) from
    /// "not owned" (id mismatch) without materializing the whole round.
    /// </summary>
    Task<string?> GetRoundOwnerIdAsync(long roundId);

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
    /// Soft-deletes a round and all its child records (scores, hole stats, round stats, shots).
    /// Returns false if the round was not found.
    /// </summary>
    Task<bool> DeleteRoundAsync(long roundId, string userId);

    /// <summary>
    /// Gets all shots for a round, grouped by ScoreId (hole).
    /// </summary>
    Task<Dictionary<long, List<ShotData>>> GetShotsByRoundIdAsync(long roundId);

    /// <summary>
    /// Loads shots for rounds that have UsingShotTracking = true and attaches them to RoundResponse objects.
    /// </summary>
    Task LoadShotsForRoundsAsync(List<RoundResponse> rounds);
}
