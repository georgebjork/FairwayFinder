using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Repositories.Interfaces;

public interface IHoleRepository : IBaseRepository
{
    public Task<List<Hole>> GetHolesForTeeAsync(long teeboxId);

    public Task<List<Hole>> GetHolesForRoundByRoundIdAsync(long roundId);
}