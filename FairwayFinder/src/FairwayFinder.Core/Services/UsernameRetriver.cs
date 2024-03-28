using FairwayFinder.Core.Helpers;
using Microsoft.AspNetCore.Http;

namespace FairwayFinder.Core.Services;

public interface IUsernameRetriever
{
    public string Username { get; }
    public string UserId { get; }
    public string Email { get; }
}

public class UsernameRetriever(IHttpContextAccessor httpContextAccessor) : IUsernameRetriever {

    public string Username => httpContextAccessor.CurrentUserName();
    public string UserId => httpContextAccessor.CurrentUserId();
    public string Email => httpContextAccessor.CurrentEmail();
}
