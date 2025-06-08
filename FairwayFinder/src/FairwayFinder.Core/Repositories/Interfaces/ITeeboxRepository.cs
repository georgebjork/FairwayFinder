using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Repositories.Interfaces;

public interface ITeeboxRepository : IBaseRepository
{
    public Task<Teebox?> GetTeeByIdAsync(long teeboxId);
    public Task<List<Teebox>> GetTeesForCourseAsync(long courseId);
    public Task<Dictionary<string, string>> GetTeesDropdownForCourseAsync(long courseId);
    public Task<int> InsertNewTeeAsync(Teebox teebox, List<Hole> holes);
    public Task<bool> UpdateTeeAsync(Teebox tee, List<Hole> holes);
}