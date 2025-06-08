using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services;

public class HoleService : IHoleService
{
    private readonly ILogger<HoleService> _logger;
    private readonly IHoleRepository _holeRepository;

    public HoleService(ILogger<HoleService> logger, IHoleRepository holeRepository)
    {
        _logger = logger;
        _holeRepository = holeRepository;
    }

    public async Task<List<Hole>> GetHolesForTeeAsync(long teeboxId, bool frontNine = false, bool backNine = false)
    {
        var holes = await _holeRepository.GetHolesForTeeAsync(teeboxId);
        
        if (frontNine) return holes.Where(x => x.hole_number <= 9).OrderBy(y => y.hole_number).ToList();
        if (backNine) return holes.Where(x => x.hole_number > 9).OrderBy(y => y.hole_number).ToList();

        return holes.OrderBy(x => x.hole_number).ToList();
    }

    public async Task<List<Hole>> GetHolesForRoundByRoundIdAsync(long roundId)
    {
        return await _holeRepository.GetHolesForRoundByRoundIdAsync(roundId);
    }
}