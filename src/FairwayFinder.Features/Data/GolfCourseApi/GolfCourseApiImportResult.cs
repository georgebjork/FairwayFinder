namespace FairwayFinder.Features.Data.GolfCourseApi;

public class GolfCourseApiImportResult
{
    public int CoursesImported { get; set; }
    public int CoursesUpdated { get; set; }
    public int CoursesSkipped { get; set; }
    public int TotalPages { get; set; }
    public int PagesProcessed { get; set; }
    public int TotalRecords { get; set; }
    public List<GolfCourseApiImportError> Errors { get; set; } = [];
}

public class GolfCourseApiImportError
{
    public int ApiCourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
