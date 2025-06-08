using FairwayFinder.Core.Features.GolfCourse.Models.FormModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Services.Interfaces;

public interface ITeeboxService
{
    Task<Teebox?> GetTeeByIdAsync(long teeboxId);
    Task<List<Teebox>> GetTeesForCourseAsync(long courseId);
    Task<Dictionary<string, string>> GetTeesDropdownForCourseAsync(long courseId);
    Task<int> AddTeeAsync(long courseId, TeeboxFormModel form);
    Task<bool> UpdateTeeAsync(long teeboxId, TeeboxFormModel form);
}
