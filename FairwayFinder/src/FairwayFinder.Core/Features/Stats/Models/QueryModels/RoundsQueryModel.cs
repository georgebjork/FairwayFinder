namespace FairwayFinder.Core.Features.Dashboard.Models.QueryModels;

public class RoundsQueryModel
{
    public long round_id { get; set; }
    public string course_name { get; set; } = "";
    public string teebox_name { get; set; } = "";
    public DateTime date_played { get; set; }
    public decimal rating { get; set; }
    public long slope { get; set; }
    public int score { get; set; }
    public int score_out { get; set; }
    public int score_in { get; set; }
    public string user_id { get; set; } = "";
    public bool using_hole_stats { get; set; } 
}