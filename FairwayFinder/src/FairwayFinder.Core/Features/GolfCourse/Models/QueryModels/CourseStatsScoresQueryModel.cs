namespace FairwayFinder.Core.Features.GolfCourse.Models.QueryModels;

public class CourseStatsScoresQueryModel
{
    public long HoleId { get; set; }
    public long TeeboxId { get; set; }
    public long CourseId { get; set; }
    
    public int HoleScore { get; set; }
    public int Par { get; set; }
}