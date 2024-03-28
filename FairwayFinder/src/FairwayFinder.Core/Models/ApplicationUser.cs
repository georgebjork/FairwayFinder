using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Core.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName;
    public string? LastName;

    [NotMapped]
    public bool IsAdmin { get; set; }
}