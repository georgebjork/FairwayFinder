using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Repositories.Interfaces;

public interface ITeeboxLookupRepository : IBaseRepository
{
    public Task<Teebox?> GetTeeByIdAsync(long teeboxId);
    public Task<List<Teebox>> GetTeesForCourseAsync(long courseId);
    public Task<Dictionary<string, string>> GetTeesDropdownForCourseAsync(long courseId);
}