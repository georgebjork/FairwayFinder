namespace FairwayFinder.Admin.Components.Pages.Stats.Components;

/// <summary>
/// One labelled value in a diagnostic metric table. Values are pre-formatted strings so the
/// table stays dumb and the page decides how each metric should read.
/// </summary>
public sealed record MetricRow(string Metric, string Value);
