namespace FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;

public class CourseStatsRoundsResponse
{
    public int RoundsCount { get; set; }
    public int RoundsNineHoleCount { get; set; }
    
    public double AvgScore { get; set; }
    public double AvgNineHoleScore { get; set; }
    
    public int HighScore { get; set; }
    public int HighNineHoleScore { get; set; }
    
    public int LowScore { get; set; }
    public int LowNineHoleScore { get; set; }
}