namespace FairwayFinder.Data.Entities;

public partial class TgtrRoundMap
{
    public long TgtrRoundMapId { get; set; }

    public int TgtrRoundId { get; set; }

    public long RoundId { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
