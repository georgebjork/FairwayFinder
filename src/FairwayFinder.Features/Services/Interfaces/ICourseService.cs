using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface ICourseService
{
    /// <summary>
    /// Searches courses by name (case-insensitive contains).
    /// </summary>
    Task<List<CourseSearchResult>> SearchCoursesAsync(string query);
    
    /// <summary>
    /// Gets all teeboxes for a course.
    /// </summary>
    Task<List<TeeboxOption>> GetTeeboxesAsync(long courseId);
    
    /// <summary>
    /// Gets all holes for a teebox, ordered by hole number.
    /// </summary>
    Task<List<HoleInfo>> GetHolesAsync(long teeboxId);
}
