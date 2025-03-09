namespace FairwayFinder.Core.Features.Scorecards.Models.QueryModels;

public class ScorecardSummaryQueryModel
{
    public long round_id { get; set; }
    public string course_name { get; set; } = "";
    public string teebox_name { get; set; } = "";
    public int score { get; set; }
    public int score_out { get; set; }
    public int score_in { get; set; }
    public int slope { get; set; }
    public decimal rating { get; set; }
    public DateTime date_played { get; set; }
    public long yardage_out { get; set; }
    public long yardage_in { get; set; }
    public long yardage_total { get; set; }
    public long par { get; set; }
    public bool using_hole_stats { get; set; }
}