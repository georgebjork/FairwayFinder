namespace FairwayFinder.Features.Rounds;

public interface IRoundService
{
    Task<List<RoundDto>> GetRoundsByUserIdAsync(string userId);
    Task<ScorecardDto?> GetRoundScorecardAsync(long roundId);
}
