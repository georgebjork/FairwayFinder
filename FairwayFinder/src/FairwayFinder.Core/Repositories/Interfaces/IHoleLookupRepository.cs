using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Repositories.Interfaces;

public interface IHoleLookupRepository : IBaseRepository
{
    public Task<List<Hole>> GetHolesForTeeAsync(long teeboxId);

}