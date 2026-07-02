namespace FairwayFinder.Features.Helpers;

/// <summary>
/// Per-category strokes-gained offsets for a golfer level, on an 18-hole-round basis.
/// Maps the REFERENCED CHARTS columns: TEE → OffTheTee, APPROACH, SHORT → AroundTheGreen, PUTT.
/// </summary>
public readonly record struct SgCategoryOffsets(
    double OffTheTee,
    double Approach,
    double AroundTheGreen,
    double Putting);
