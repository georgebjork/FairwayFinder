using System;
using System.Collections.Generic;

namespace FairwayFinder.Data.Entities;

public partial class UserInvitation
{
    public int Id { get; set; }

    public string InvitationIdentifier { get; set; } = null!;

    public string SentToEmail { get; set; } = null!;

    public string SentByUser { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateOnly? ClaimedOn { get; set; }

    public DateOnly ExpiresOn { get; set; }

    public DateOnly CreatedOn { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;
}
