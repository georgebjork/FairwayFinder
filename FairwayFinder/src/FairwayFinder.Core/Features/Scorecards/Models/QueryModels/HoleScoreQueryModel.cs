﻿namespace FairwayFinder.Core.Features.Scorecards.Models.QueryModels;

public class HoleScoreQueryModel
{
    public long score_id { get; set; }
    public long hole_id { get; set; }
    public short hole_score { get; set; }
    public long yardage { get; set; }
    public long handicap { get; set; }
    public long par { get; set; }
    public long hole_number { get; set; }
}