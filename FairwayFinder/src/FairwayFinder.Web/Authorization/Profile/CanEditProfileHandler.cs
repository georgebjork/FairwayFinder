using FairwayFinder.Core.Identity.Settings;
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
        
        /*
         // Ensure the Resource is an HttpContext. In many cases, this will be true when using endpoint routing.
           if (context.Resource is HttpContext httpContext)
           {
               // Assume the user id is being passed as a route parameter named "userId"
               RouteValueDictionary routeValues = httpContext.GetRouteData()?.Values;
               if (routeValues != null && routeValues.TryGetValue("userId", out object routeUserId))
               {
                   string userIdFromRoute = routeUserId.ToString();

                   // Retrieve the current user id from the claims
                   string currentUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                   // If both IDs match, the requirement is fulfilled.
                   if (!string.IsNullOrEmpty(currentUserId) && currentUserId.Equals(userIdFromRoute))
                   {
                       context.Succeed(requirement);
                   }
               }
           }
         */

        return Task.CompletedTask; 
    }
}