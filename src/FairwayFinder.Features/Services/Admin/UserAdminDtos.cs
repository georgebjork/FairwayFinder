using FairwayFinder.Features.Data;
using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Services.Admin;

public class UserAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName => DisplayNameHelper.Build(FirstName, LastName, userName: null);
    public List<string> Roles { get; set; } = new();
    public bool IsAdmin => Roles.Contains("Admin");
    public bool IsLockedOut { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public bool IsSearchHidden { get; set; }

    /// <summary>Public stats page settings (fairwayfinder.pro/golf/{Slug}).</summary>
    public PublicProfileSettingsRequest PublicProfile { get; set; } = new();
    public DateTime CreatedOn { get; set; }
}

public class UpdateUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
