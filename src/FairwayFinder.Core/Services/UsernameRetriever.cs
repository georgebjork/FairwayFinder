using FairwayFinder.Core.Helpers;
using Microsoft.AspNetCore.Http;

namespace FairwayFinder.Core.Services;

public interface IUsernameRetriever
{
    public string Username { get; }
    public string UserId { get; }
    public string Email { get; }
    public string FullName { get; }
    public string UserInitials { get; }
    public string FirstInitial { get;  }
}

public class UsernameRetriever(IHttpContextAccessor httpContextAccessor) : IUsernameRetriever
{

    public string Username => httpContextAccessor.CurrentUserName();
    public string UserId => httpContextAccessor.CurrentUserId();
    public string Email => httpContextAccessor.CurrentEmail();
    public string FullName => httpContextAccessor.CurrentFullName();

    public string UserInitials
    {
        get
        {
            var fullName = httpContextAccessor.CurrentFullName();
            var names = fullName.Split(' ');

            switch (names.Length)
            {
                // Check if we have at least two elements for first name and last name
                case >= 2:
                {
                    // Get the first character of the first name and the first character of the last name
                    var initials = names[0][0].ToString() + names[1][0].ToString();
                    return initials.ToUpper(); // Return initials in uppercase
                }
                // If there is only one name part available
                case 1:
                    // Return the first character of the available name
                    return names[0][0].ToString().ToUpper();
                default:
                    // Return "Unknown" or any default value if no names are available
                    return "Unknown";
            }
        }
    }

    public string FirstInitial
    {
        get
        {
            var fullName = httpContextAccessor.CurrentFullName();
            
            return fullName.Length switch
            {
                >= 1 => fullName[0].ToString().ToUpper(),
                _ => "Unknown"
            };
        }
    }
}