﻿using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Scorecards.Models.ViewModels;

public class ScorecardViewModel
{
    public ScorecardSummaryQueryModel ScorecardSummary { get; set; } = new();
    public ScorecardRoundStats ScorecardRoundStats { get; set; } = new();
    public List<HoleScoreQueryModel> Holes { get; set; } = [];
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
}