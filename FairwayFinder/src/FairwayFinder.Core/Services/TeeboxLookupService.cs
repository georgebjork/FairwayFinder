using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services;

public class TeeboxLookupService
{
    private readonly ILogger<TeeboxLookupService> _logger;
    private readonly ITeeboxLookupRepository _teeboxLookupRepository;

    public TeeboxLookupService(ILogger<TeeboxLookupService> logger, ITeeboxLookupRepository teeboxLookupRepository)
    {
        _logger = logger;
        _teeboxLookupRepository = teeboxLookupRepository;
    }
    
    public async Task<Teebox?> GetTeeByIdAsync(long teeboxId)
    {
        return await _teeboxLookupRepository.GetTeeByIdAsync(teeboxId);
    }
     
    public async Task<List<Teebox>> GetTeesForCourseAsync(long courseId)
    {
        return await _teeboxLookupRepository.GetTeesForCourseAsync(courseId);
    }
    
    public async Task<Dictionary<string, string>> GetTeesDropdownForCourseAsync(long courseId)
    {
        return await _teeboxLookupRepository.GetTeesDropdownForCourseAsync(courseId);
    }
}