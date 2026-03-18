namespace FairwayFinder.Data.Entities;

public partial class GolfCourseApiCourseMap
{
    public long GolfCourseApiCourseMapId { get; set; }

    public int ApiCourseId { get; set; }

    public long CourseId { get; set; }

    // Navigation properties
    public virtual Course Course { get; set; } = null!;
}
