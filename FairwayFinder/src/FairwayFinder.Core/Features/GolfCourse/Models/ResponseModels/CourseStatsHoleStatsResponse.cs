namespace FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;

public class CourseStatsHoleStatsResponse
{
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int Handicap { get; set; }
    public double AverageScore { get; set; }
    public int BestScore { get; set; }
    public int WorseScore { get; set; }
    public int TimesPlayed { get; set; }
}