namespace FairwayFinder.Core.Features.GolfCourse.Models;

public class CourseStatsRequest
{
    public long CourseId { get; set; }
    public long TeeboxId { get; set; }
    public int Year { get; set; }
    public string UserId { get; set; } = string.Empty;
}