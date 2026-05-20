using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public PreferredTees PreferredTees { get; set; }

    // When true, the user is excluded from user/friend search results.
    public bool IsSearchHidden { get; set; }

    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }

    [NotMapped]
    public bool IsAdmin { get; set; }
}