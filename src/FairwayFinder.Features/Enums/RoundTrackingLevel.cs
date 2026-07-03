using System.Text.Json.Serialization;

namespace FairwayFinder.Features.Enums;

/// <summary>
/// How much detail a round was recorded with. Derived from the round's
/// UsingShotTracking / UsingHoleStats flags — see <see cref="Data.RoundResponse.TrackingLevel"/>.
/// Serialized as its name (e.g. "ShotTracked") in API responses.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoundTrackingLevel
{
    /// <summary>Score only.</summary>
    Basic = 0,

    /// <summary>Per-hole stats (fairways, greens, putts), no shot data.</summary>
    HoleStats = 1,

    /// <summary>Full shot-by-shot tracking (strokes gained available).</summary>
    ShotTracked = 2
}

public static class RoundTracking
{
    /// <summary>
    /// Classifies a round's detail level from its tracking flags. Single source of truth for the
    /// Basic / HoleStats / ShotTracked tiers (shot-tracked rounds also set UsingHoleStats).
    /// </summary>
    public static RoundTrackingLevel Classify(bool usingShotTracking, bool usingHoleStats) =>
        usingShotTracking ? RoundTrackingLevel.ShotTracked
        : usingHoleStats ? RoundTrackingLevel.HoleStats
        : RoundTrackingLevel.Basic;
}
