namespace FairwayFinder.Core.Features.GolfCourse.Models.QueryModels;

public class CourseStatsRoundQueryModel
{
    public long CourseId { get; set; }
    public long TeeboxId { get; set; }
    
    public int Score { get; set; }
    public bool FullRound { get; set; }
}