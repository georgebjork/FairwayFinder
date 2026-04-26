using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Extensions;
using FairwayFinder.Api.Validators;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Api.Endpoints;

public static class FriendEndpoints
{
    public static WebApplication MapFriendEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/friends")
            .WithTags("Friends")
            .RequireAuthorization();

        group.MapGet("/", async (HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            var friends = await friendService.GetFriendsAsync(userId);
            return Results.Ok(friends);
        });

        group.MapGet("/requests/incoming", async (HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            var requests = await friendService.GetIncomingRequestsAsync(userId);
            return Results.Ok(requests);
        });

        group.MapGet("/requests/incoming/count", async (HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            var count = await friendService.GetIncomingRequestCountAsync(userId);
            return Results.Ok(count);
        });

        group.MapGet("/requests/outgoing", async (HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            var requests = await friendService.GetOutgoingRequestsAsync(userId);
            return Results.Ok(requests);
        });

        group.MapGet("/search", async (string query, int? take, HttpContext ctx, IFriendService friendService) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return Results.BadRequest(new { error = "query must be at least 2 characters." });
            }

            var userId = ctx.User.GetUserId();
            var results = await friendService.SearchUsersAsync(userId, query, take ?? 20);
            return Results.Ok(results);
        });

        group.MapPost("/requests", async (SendFriendRequestRequest request, HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            try
            {
                var newId = await friendService.SendRequestAsync(userId, request.AddresseeUserId);
                return Results.Created($"/api/friends/requests/{newId}", newId);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).AddEndpointFilter<ValidationFilter<SendFriendRequestRequest>>();

        group.MapPut("/requests/{friendshipId:long}/accept", async (long friendshipId, HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            var ok = await friendService.AcceptRequestAsync(friendshipId, userId);
            return ok ? Results.NoContent() : throw new NotFoundException("FriendRequest", friendshipId);
        });

        group.MapPut("/requests/{friendshipId:long}/reject", async (long friendshipId, HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            var ok = await friendService.RejectRequestAsync(friendshipId, userId);
            return ok ? Results.NoContent() : throw new NotFoundException("FriendRequest", friendshipId);
        });

        group.MapDelete("/requests/{friendshipId:long}", async (long friendshipId, HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            var ok = await friendService.CancelRequestAsync(friendshipId, userId);
            return ok ? Results.NoContent() : throw new NotFoundException("FriendRequest", friendshipId);
        });

        group.MapDelete("/{friendshipId:long}", async (long friendshipId, HttpContext ctx, IFriendService friendService) =>
        {
            var userId = ctx.User.GetUserId();
            var ok = await friendService.RemoveFriendAsync(friendshipId, userId);
            return ok ? Results.NoContent() : throw new NotFoundException("Friendship", friendshipId);
        });

        group.MapGet("/{publicId:guid}/rounds", async (
            Guid publicId,
            HttpContext ctx,
            IFriendService friendService,
            IProfileService profileService,
            IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            var targetUserId = await EnsureFriendAccessAsync(publicId, userId, friendService, profileService);

            var rounds = await roundService.GetRoundsByUserIdAsync(targetUserId);
            return Results.Ok(rounds);
        });

        group.MapGet("/{publicId:guid}/stats", async (
            Guid publicId,
            HttpContext ctx,
            IFriendService friendService,
            IProfileService profileService,
            IStatsService statsService) =>
        {
            var userId = ctx.User.GetUserId();
            var targetUserId = await EnsureFriendAccessAsync(publicId, userId, friendService, profileService);

            var stats = await statsService.GetUserStatsAsync(targetUserId);
            return Results.Ok(stats);
        });

        return app;
    }

    private static async Task<string> EnsureFriendAccessAsync(
        Guid publicId,
        string viewerUserId,
        IFriendService friendService,
        IProfileService profileService)
    {
        var targetUserId = await profileService.GetUserIdByPublicIdIgnoringVisibilityAsync(publicId);
        if (targetUserId is null)
            throw new NotFoundException("Profile", publicId);

        if (targetUserId == viewerUserId)
            return targetUserId;

        var areFriends = await friendService.AreFriendsAsync(viewerUserId, targetUserId);
        if (!areFriends)
            throw new NotFoundException("Profile", publicId);

        return targetUserId;
    }
}
