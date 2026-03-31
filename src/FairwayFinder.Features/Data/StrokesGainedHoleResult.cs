namespace FairwayFinder.Features.Data;

/// <summary>
/// Strokes gained breakdown for a single hole.
/// </summary>
public class StrokesGainedHoleResult
{
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int Score { get; set; }

    public double SgTotal { get; set; }
    public double SgPutting { get; set; }
    public double SgOffTheTee { get; set; }
    public double SgApproach { get; set; }
    public double SgAroundTheGreen { get; set; }

    public List<ShotData> Shots { get; set; } = new();
}
