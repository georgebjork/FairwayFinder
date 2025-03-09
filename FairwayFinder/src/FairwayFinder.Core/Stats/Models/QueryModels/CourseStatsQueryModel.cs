namespace FairwayFinder.Core.Stats.Models.QueryModels;

public class CourseStatsQueryModel
{
    public double average_score { get; set; }
    public int low_score { get; set; }
    public int high_score { get; set; }
    public string teebox_name { get; set; } = "";
}