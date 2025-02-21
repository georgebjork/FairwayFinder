namespace FairwayFinder.Core.Features.Scorecards.Models.QueryModels;

public class HoleStatsQueryModel
{
    public long hole_number { get; set; }
    public long hole_stats_id { get; set; }
    public long round_id { get; set; }
    public long score_id { get; set; }
    public long hole_id { get; set; }
    public bool? hit_fairway { get; set; }
    public string miss_fairway_type_string { get; set; } = "";
    public int? miss_fairway_type { get; set; }
    public bool? hit_green { get; set; }
    public string miss_green_type_string { get; set; } = "";
    public int? miss_green_type { get; set; }
    public short? number_of_putts { get; set; }
    public int? approach_yardage { get; set; }
}