using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services;

public class TeeboxService : ITeeboxService
{
    private readonly ILogger<TeeboxService> _logger;
    private readonly ITeeboxRepository _teeboxRepository;

    public TeeboxService(ILogger<TeeboxService> logger, ITeeboxRepository teeboxRepository)
    {
        _logger = logger;
        _teeboxRepository = teeboxRepository;
    }
    
    public async Task<Teebox?> GetTeeByIdAsync(long teeboxId)
    {
        return await _teeboxRepository.GetTeeByIdAsync(teeboxId);
    }
     
    public async Task<List<Teebox>> GetTeesForCourseAsync(long courseId)
    {
        return await _teeboxRepository.GetTeesForCourseAsync(courseId);
    }
    
    public async Task<Dictionary<string, string>> GetTeesDropdownForCourseAsync(long courseId)
    {
        return await _teeboxRepository.GetTeesDropdownForCourseAsync(courseId);
    }
}