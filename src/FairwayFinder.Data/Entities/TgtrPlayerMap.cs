using System;

namespace FairwayFinder.Data.Entities;

public partial class TgtrPlayerMap
{
    public long TgtrPlayerMapId { get; set; }

    public int TgtrPlayerId { get; set; }

    public string UserId { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
