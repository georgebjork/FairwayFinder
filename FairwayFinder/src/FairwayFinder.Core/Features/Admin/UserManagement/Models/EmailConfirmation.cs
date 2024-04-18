using Dapper.Contrib.Extensions;

namespace FairwayFinder.Core.Features.Admin.UserManagement.Models;

[Table("email_confirmation")]
public class EmailConfirmation
{
    public int id { get; set; }
    public string user_id { get; set; }
    public string confirmation_id { get; set; } = "";
    public string sent_to_email { get; set; } = "";
    public bool is_deleted { get; set; }
    public bool is_confirmed { get; set; }
    public DateTime? confirmed_on { get; set; }
    public DateTime expires_on { get; set; }
    public DateTime created_on { get; set; }
    public string created_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public string updated_by { get; set; } = "";

    [Computed] 
    public string ConfirmationUrl { get; set; } = "";
}