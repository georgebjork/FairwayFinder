using System;

using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

public partial class TgtrPlayerMap : IAuditable
{
    public long TgtrPlayerMapId { get; set; }

    public int TgtrPlayerId { get; set; }

    public string UserId { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
