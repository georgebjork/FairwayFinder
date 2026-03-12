namespace FairwayFinder.Features.Services.Admin;

public class UserAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public List<string> Roles { get; set; } = new();
    public bool IsAdmin => Roles.Contains("Admin");
    public bool IsLockedOut { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class UpdateUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
