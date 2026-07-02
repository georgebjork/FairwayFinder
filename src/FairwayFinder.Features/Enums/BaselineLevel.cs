using System.Text.Json.Serialization;

namespace FairwayFinder.Features.Enums;

/// <summary>
/// The golfer skill level whose expected-strokes baseline is used when computing strokes gained.
/// <c>Scratch</c> is the default. Handicap levels come from the reference baseline data; <c>Tour</c>
/// is the legacy in-code baseline. Values are stable (persisted as a per-user preference) and use
/// the handicap number where applicable. Serialized as its name (e.g. "Hcp10") in API payloads.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BaselineLevel
{
    Scratch = 0,
    Hcp5 = 5,
    Hcp10 = 10,
    Hcp15 = 15,
    Hcp20 = 20,
    Hcp25 = 25,
    Tour = -1
}

public static class BaselineLevelExtensions
{
    /// <summary>Human-friendly label for dropdowns.</summary>
    public static string DisplayName(this BaselineLevel level) => level switch
    {
        BaselineLevel.Tour => "Tour",
        BaselineLevel.Scratch => "Scratch",
        BaselineLevel.Hcp5 => "5 Handicap",
        BaselineLevel.Hcp10 => "10 Handicap",
        BaselineLevel.Hcp15 => "15 Handicap",
        BaselineLevel.Hcp20 => "20 Handicap",
        BaselineLevel.Hcp25 => "25 Handicap",
        _ => level.ToString()
    };

    /// <summary>All levels in display order (best to worst), for populating selectors.</summary>
    public static IReadOnlyList<BaselineLevel> AllInDisplayOrder { get; } = new[]
    {
        BaselineLevel.Tour,
        BaselineLevel.Scratch,
        BaselineLevel.Hcp5,
        BaselineLevel.Hcp10,
        BaselineLevel.Hcp15,
        BaselineLevel.Hcp20,
        BaselineLevel.Hcp25
    };
}
