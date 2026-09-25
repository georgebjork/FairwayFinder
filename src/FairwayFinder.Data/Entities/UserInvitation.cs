using System;
using System.Collections.Generic;

using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

public partial class UserInvitation : IAuditable
{
    public int Id { get; set; }

    public string InvitationIdentifier { get; set; } = null!;

    public string SentToEmail { get; set; } = null!;

    public string SentByUser { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime? ClaimedOn { get; set; }

    public DateTime ExpiresOn { get; set; }

    public DateTime CreatedOn { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;
}
