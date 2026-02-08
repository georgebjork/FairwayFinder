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
}
