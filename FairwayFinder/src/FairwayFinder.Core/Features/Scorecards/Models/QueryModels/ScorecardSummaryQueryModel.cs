namespace FairwayFinder.Core.Features.Scorecards.Models.QueryModels;

public class ScorecardSummaryQueryModel
{
    public string course_name { get; set; } = "";
    public string teebox_name { get; set; } = "";
    public int score { get; set; }
    public int slope { get; set; }
    public decimal rating { get; set; }
    public DateTime date_played { get; set; }
}