using FairwayFinder.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace FairwayFinder.Identity.Authorization.Profile;

public class CanEditProfileRequirement : IAuthorizationRequirement {}

public class EditProfileAuthorization(IUsernameRetriever usernameRetriever) : AuthorizationHandler<CanEditProfileRequirement, string> {


    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CanEditProfileRequirement requirement, string resource)
    {
        // Make the sure the resource isn't null or empty and this user is even authenticated
        if (string.IsNullOrEmpty(resource) && !context.User.Identity!.IsAuthenticated)
        {
            return Task.CompletedTask;
        }
        
        // Check if the current user ID matches the resource
        if (usernameRetriever.UserId == resource)
        {
            context.Succeed(requirement); 
        }

        return Task.CompletedTask; 
    }
}