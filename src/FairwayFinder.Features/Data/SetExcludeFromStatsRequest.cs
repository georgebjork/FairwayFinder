namespace FairwayFinder.Features.Data;

/// <summary>
/// Request DTO for flagging a round as excluded from (or included back into) the owner's stats.
/// The round stays in the owner's round list either way.
/// </summary>
public class SetExcludeFromStatsRequest
{
    public bool ExcludeFromStats { get; set; }
}
