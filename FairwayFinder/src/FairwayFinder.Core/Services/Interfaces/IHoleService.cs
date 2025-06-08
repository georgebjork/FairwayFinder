using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Services.Interfaces;

public interface IHoleService
{
    Task<List<Hole>> GetHolesForTeeAsync(long teeboxId, bool frontNine = false, bool backNine = false);
    Task<List<Hole>> GetHolesForRoundByRoundIdAsync(long roundId);
}
