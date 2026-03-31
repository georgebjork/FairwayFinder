namespace FairwayFinder.Features.Data;

/// <summary>
/// A single data point for SG trend charting.
/// </summary>
public class StrokesGainedTrendPoint
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public double Value { get; set; }
    public double? MovingAverage { get; set; }
}
