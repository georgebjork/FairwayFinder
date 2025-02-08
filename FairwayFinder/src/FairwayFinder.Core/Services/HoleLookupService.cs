using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services;

public class HoleLookupService
{
    private readonly ILogger<HoleLookupService> _logger;
    private readonly IHoleLookupRepository _holeLookupRepository;

    public HoleLookupService(ILogger<HoleLookupService> logger, IHoleLookupRepository holeLookupRepository)
    {
        _logger = logger;
        _holeLookupRepository = holeLookupRepository;
    }

    public async Task<List<Hole>> GetHolesForTeeAsync(long teeboxId)
    {
        return await _holeLookupRepository.GetHolesForTeeAsync(teeboxId);
    }

    public async Task<List<Hole>> GetHolesForRoundByRoundIdAsync(long roundId)
    {
        return await _holeLookupRepository.GetHolesForRoundByRoundIdAsync(roundId);
    }
}