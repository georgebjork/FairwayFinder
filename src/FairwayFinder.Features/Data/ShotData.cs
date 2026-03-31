using FairwayFinder.Features.Enums;

namespace FairwayFinder.Features.Data;

/// <summary>
/// A single shot's data — used for both input (round entry) and display (round view).
/// </summary>
public class ShotData
{
    public long ShotId { get; set; }
    public int ShotNumber { get; set; }

    public int StartDistance { get; set; }
    public string StartDistanceUnit { get; set; } = DistanceUnit.Yards;
    public LieType StartLie { get; set; }

    public int? EndDistance { get; set; }
    public string? EndDistanceUnit { get; set; }
    public LieType? EndLie { get; set; }

    public int PenaltyStrokes { get; set; }

    // Computed (set by calculator, not by user)
    public double? StrokesGained { get; set; }
    public ShotCategory? Category { get; set; }
}
