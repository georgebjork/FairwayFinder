namespace FairwayFinder.Data.Entities;

public partial class RoundStat
{
    public long RoundStatsId { get; set; }

    public long RoundId { get; set; }

    public int HoleInOne { get; set; }

    public int DoubleEagles { get; set; }

    public int Eagles { get; set; }

    public int Birdies { get; set; }

    public int Pars { get; set; }

    public int Bogies { get; set; }

    public int DoubleBogies { get; set; }

    public int TripleOrWorse { get; set; }

    // Strokes Gained totals (null for non-shot-tracked rounds)
    public double? SgTotal { get; set; }
    public double? SgPutting { get; set; }
    public double? SgTeeToGreen { get; set; }
    public double? SgOffTheTee { get; set; }
    public double? SgApproach { get; set; }
    public double? SgAroundTheGreen { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Round Round { get; set; } = null!;
}
