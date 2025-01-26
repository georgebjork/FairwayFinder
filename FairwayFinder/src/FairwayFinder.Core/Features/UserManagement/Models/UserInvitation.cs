using Dapper.Contrib.Extensions;

namespace FairwayFinder.Core.UserManagement.Models;

[Table("user_invitation")]
public class UserInvitation
{
    public int id { get; set; }
    public string invitation_identifier { get; set; } = "";
    public string sent_to_email { get; set; } = "";
    public string sent_by_user { get; set; } = "";
    public bool is_deleted { get; set; }
    public DateTime? claimed_on { get; set; }
    public DateTime expires_on { get; set; }
    public DateTime created_on { get; set; }
    public string created_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public string updated_by { get; set; } = "";

    [Computed] 
    public string InviteUrl { get; set; } = "";
}