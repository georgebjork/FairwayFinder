using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

public partial class TgtrRoundMap : IAuditable
{
    public long TgtrRoundMapId { get; set; }

    public int TgtrRoundId { get; set; }

    public long RoundId { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Round Round { get; set; } = null!;
}
