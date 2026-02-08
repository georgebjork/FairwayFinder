using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IRoundService
{
    Task<List<RoundResponse>> GetRoundsByUserIdAsync(string userId);
    
    
    Task<ScorecardDto?> GetRoundScorecardAsync(long roundId);
}
