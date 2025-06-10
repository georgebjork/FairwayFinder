namespace FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;

public class CourseStatsResponse
{
    public CourseStatsScoreCountsResponse ScoreCounts { get; set; } = new();
    public CourseStatsRoundsResponse RoundScores { get; set; } = new();
    public List<CourseStatsHoleStatsResponse> HoleStats { get; set; } = [];
}