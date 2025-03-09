namespace FairwayFinder.Core.Stats.Models.QueryModels;

public class AverageScoreByParQueryModel
{
    public double average_score_fairway_hit { get; set; }
    public int average_score_fairway_hit_count { get; set; }
    public double average_score_fairway_miss { get; set; }
    public int average_score_fairway_miss_count { get; set; }
    public double average_score_green_hit { get; set; }
    public int average_score_green_hit_count { get; set; }
    public double average_score_green_miss { get; set; }
    public int average_score_green_miss_count { get; set; }
    public double average_score_both_hit { get; set; }
    public int average_score_both_hit_count { get; set; }
    public double average_score_both_miss { get; set; }
    public int average_score_both_miss_count { get; set; }
    public double average_score { get; set; }
    public int average_score_count { get; set; }
    public int par { get; set; }
}