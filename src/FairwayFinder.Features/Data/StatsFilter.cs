namespace FairwayFinder.Features.Data;

/// <summary>
/// Filter options for stats queries.
/// </summary>
public class StatsFilter
{
    /// <summary>
    /// Filter by round type: null = all, true = 18-hole only, false = 9-hole only
    /// </summary>
    public bool? FullRoundOnly { get; set; }
    
    /// <summary>
    /// Filter by start date (inclusive). Null = no lower bound.
    /// </summary>
    public DateOnly? StartDate { get; set; }
    
    /// <summary>
    /// Filter by end date (inclusive). Null = no upper bound.
    /// </summary>
    public DateOnly? EndDate { get; set; }
    
    /// <summary>
    /// Returns true if any filter is applied.
    /// </summary>
    public bool HasFilters => FullRoundOnly.HasValue || StartDate.HasValue || EndDate.HasValue;
}
