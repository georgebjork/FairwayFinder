using System.ComponentModel.DataAnnotations.Schema;
using FairwayFinder.Shared;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Identity;

public class ApplicationUser : IdentityUser, IAuditable
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public PreferredTees PreferredTees { get; set; }

    // Default golfer level used to compute strokes gained (stored as the BaselineLevel enum's
    // int value; 0 = Scratch). Kept as int to avoid an Identity → Features dependency.
    public int SgBaselineLevel { get; set; }

    // When true, the user is excluded from user/friend search results.
    public bool IsSearchHidden { get; set; }

    // The golfer's public username, used only to find friends — never to sign in. It is not
    // Identity's UserName, which holds the email and is what the admin login signs in with.
    // Always lowercase (see UsernameRules), unique, and changeable by the golfer at any time.
    public string Handle { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }

    [NotMapped]
    public bool IsAdmin { get; set; }
}