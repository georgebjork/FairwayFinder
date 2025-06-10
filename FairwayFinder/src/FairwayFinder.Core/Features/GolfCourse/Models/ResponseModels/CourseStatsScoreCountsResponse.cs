namespace FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;

public class CourseStatsScoreCountsResponse
{
    public int HoleInOnes { get; set; }
    public int Albatross { get; set; }
    public int Eagle { get; set; }
    public int Birdie { get; set; }
    public int Par { get; set; }
    public int Bogey { get; set; }
    public int DoubleBogey { get; set; }
    public int Worse { get; set; }
}