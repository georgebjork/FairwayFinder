using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace FairwayFinder.Web.Authorization.Profile;

public class CanEditProfileRequirement : IAuthorizationRequirement {}

public class CanEditProfileHandler : AuthorizationHandler<CanEditProfileRequirement, string>
{

    private readonly IUsernameRetriever _usernameRetriever;

    public CanEditProfileHandler(IUsernameRetriever usernameRetriever)
    {
        _usernameRetriever = usernameRetriever;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CanEditProfileRequirement requirement, string resource)
    {
        // Make the sure the resource isn't null or empty and this user is even authenticated
        if (string.IsNullOrEmpty(resource) && !context.User.Identity!.IsAuthenticated)
        {
            return Task.CompletedTask;
        }
        
        // Check if the current user ID matches the resource
        if (_usernameRetriever.UserId == resource || context.User.IsInRole(Roles.Admin))
        {
            context.Succeed(requirement); 
        }

        return Task.CompletedTask; 
    }
}