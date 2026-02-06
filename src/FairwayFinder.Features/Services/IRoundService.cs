using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services;

public interface IRoundService
{
    Task<List<RoundDto>> GetRoundsByUserIdAsync(string userId);
    Task<ScorecardDto?> GetRoundScorecardAsync(long roundId);
}
