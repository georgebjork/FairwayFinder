namespace FairwayFinder.Features.Data;

// ── Course DTOs ──────────────────────────────────────────────

/// <summary>
/// Row item for the course list grid.
/// </summary>
public class CourseListItem
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public int TeeboxCount { get; set; }
}

/// <summary>
/// Full course detail including its teeboxes.
/// </summary>
public class CourseDetailResponse
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public List<TeeboxSummary> Teeboxes { get; set; } = new();
}

/// <summary>
/// Teebox row in the course detail grid.
/// </summary>
public class TeeboxSummary
{
    public long TeeboxId { get; set; }
    public string TeeboxName { get; set; } = string.Empty;
    public int Par { get; set; }
    public decimal Rating { get; set; }
    public int Slope { get; set; }
    public int YardageOut { get; set; }
    public int YardageIn { get; set; }
    public int YardageTotal { get; set; }
    public bool IsNineHole { get; set; }
    public bool IsWomens { get; set; }
}

/// <summary>
/// Full teebox detail including hole-by-hole data.
/// </summary>
public class TeeboxDetailResponse
{
    public long TeeboxId { get; set; }
    public long CourseId { get; set; }
    public string TeeboxName { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public int Slope { get; set; }
    public bool IsNineHole { get; set; }
    public bool IsWomens { get; set; }
    public List<HoleInfo> Holes { get; set; } = new();
}

// ── Request DTOs ─────────────────────────────────────────────

/// <summary>
/// Request to create or update a course.
/// </summary>
public class SaveCourseRequest
{
    public long? CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Request to create or update a teebox with hole-by-hole data.
/// Par, YardageOut, YardageIn, YardageTotal are computed from hole data.
/// </summary>
public class SaveTeeboxRequest
{
    public long? TeeboxId { get; set; }
    public long CourseId { get; set; }
    public string TeeboxName { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public int Slope { get; set; }
    public bool IsNineHole { get; set; }
    public bool IsWomens { get; set; }
    public List<HoleEntry> Holes { get; set; } = new();
}

/// <summary>
/// A single hole's data for teebox creation/editing.
/// </summary>
public class HoleEntry
{
    public int HoleNumber { get; set; }
    public int Par { get; set; } = 4;
    public int Yardage { get; set; }
    public int Handicap { get; set; }
}
