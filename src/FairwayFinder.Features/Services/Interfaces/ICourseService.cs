using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface ICourseService
{
    // ── Existing read methods ────────────────────────────────

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

    // ── Course CRUD ──────────────────────────────────────────

    /// <summary>
    /// Gets all courses with teebox counts for the management list.
    /// </summary>
    Task<List<CourseListItem>> GetAllCoursesAsync();

    /// <summary>
    /// Gets full course detail including teeboxes.
    /// </summary>
    Task<CourseDetailResponse?> GetCourseDetailAsync(long courseId);

    /// <summary>
    /// Creates a new course. Returns the new course ID.
    /// </summary>
    Task<long> CreateCourseAsync(SaveCourseRequest request, string userId);

    /// <summary>
    /// Updates an existing course.
    /// </summary>
    Task<bool> UpdateCourseAsync(SaveCourseRequest request, string userId);

    /// <summary>
    /// Soft-deletes a course and all its teeboxes/holes.
    /// </summary>
    Task<bool> DeleteCourseAsync(long courseId, string userId);

    // ── Teebox CRUD ──────────────────────────────────────────

    /// <summary>
    /// Gets full teebox detail including hole-by-hole data.
    /// </summary>
    Task<TeeboxDetailResponse?> GetTeeboxDetailAsync(long teeboxId);

    /// <summary>
    /// Creates a new teebox with holes. Returns the new teebox ID.
    /// </summary>
    Task<long> CreateTeeboxAsync(SaveTeeboxRequest request, string userId);

    /// <summary>
    /// Updates a teebox and its holes.
    /// </summary>
    Task<bool> UpdateTeeboxAsync(SaveTeeboxRequest request, string userId);

    /// <summary>
    /// Soft-deletes a teebox and all its holes.
    /// </summary>
    Task<bool> DeleteTeeboxAsync(long teeboxId, string userId);
}
