using System.Security.Claims;
using FairwayFinder.Core.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace FairwayFinder.Web.Authorization.ScorecardManagement;

public class CanEditScorecardRequirement : IAuthorizationRequirement {}

public class CanEditScorecardHandler : AuthorizationHandler<CanEditScorecardRequirement, long>
{
    private readonly ScorecardAuthorizationService _scorecardAuthorization;

    public CanEditScorecardHandler(ScorecardAuthorizationService scorecardAuthorization)
    {
        _scorecardAuthorization = scorecardAuthorization;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CanEditScorecardRequirement requirement, long roundId)
    {
        // Ensure the user is authenticated
        if (!context.User.Identity!.IsAuthenticated)
        {
            return;
        }

        // Extract the user ID from claims
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Check if the user can edit the scorecard
        if (await _scorecardAuthorization.CanEditScorecard(roundId, userId))
        {
            context.Succeed(requirement);
        }
    }
}
