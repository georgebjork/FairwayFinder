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

    public async Task<List<Hole>> GetHolesForTeeAsync(long teeboxId, bool frontNine = false, bool backNine = false)
    {
        var holes = await _holeLookupRepository.GetHolesForTeeAsync(teeboxId);
        
        if (frontNine) return holes.Where(x => x.hole_number <= 9).OrderBy(y => y.hole_number).ToList();
        if (backNine) return holes.Where(x => x.hole_number > 9).OrderBy(y => y.hole_number).ToList();

        return holes;
    }

    public async Task<List<Hole>> GetHolesForRoundByRoundIdAsync(long roundId)
    {
        return await _holeLookupRepository.GetHolesForRoundByRoundIdAsync(roundId);
    }
}