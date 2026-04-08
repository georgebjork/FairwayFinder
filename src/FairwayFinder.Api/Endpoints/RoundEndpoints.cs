using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Extensions;
using FairwayFinder.Api.Validators;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Api.Endpoints;

public static class RoundEndpoints
{
    public static WebApplication MapRoundEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/rounds")
            .WithTags("Rounds")
            .RequireAuthorization();

        group.MapGet("/", async (HttpContext ctx, IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            var rounds = await roundService.GetRoundsByUserIdAsync(userId);
            return Results.Ok(rounds);
        });

        group.MapGet("/details", async (HttpContext ctx, IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            var rounds = await roundService.GetRoundsWithDetailsAsync(userId);
            return Results.Ok(rounds);
        });

        group.MapGet("/{roundId:long}", async (long roundId, HttpContext ctx, IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            await EnsureRoundAccess(roundService, roundId, userId);

            var round = await roundService.GetRoundByIdAsync(roundId);
            if (round is null)
                throw new NotFoundException("Round", roundId);

            return Results.Ok(round);
        });

        group.MapPost("/", async (CreateRoundRequest request, HttpContext ctx, IRoundService roundService) =>
        {
            request.UserId = ctx.User.GetUserId();
            var newId = await roundService.CreateRoundAsync(request);
            return Results.Created($"/api/rounds/{newId}", newId);
        }).AddEndpointFilter<ValidationFilter<CreateRoundRequest>>();

        group.MapPut("/{roundId:long}", async (long roundId, UpdateRoundRequest request, HttpContext ctx, IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            request.UserId = userId;
            request.RoundId = roundId;

            await EnsureRoundAccess(roundService, roundId, userId);

            var success = await roundService.UpdateRoundAsync(request);
            return success ? Results.NoContent() : throw new NotFoundException("Round", roundId);
        }).AddEndpointFilter<ValidationFilter<UpdateRoundRequest>>();

        group.MapDelete("/{roundId:long}", async (long roundId, HttpContext ctx, IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            await EnsureRoundAccess(roundService, roundId, userId);

            var success = await roundService.DeleteRoundAsync(roundId, userId);
            return success ? Results.NoContent() : throw new NotFoundException("Round", roundId);
        });

        group.MapGet("/{roundId:long}/shots", async (long roundId, HttpContext ctx, IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            await EnsureRoundAccess(roundService, roundId, userId);

            var shots = await roundService.GetShotsByRoundIdAsync(roundId);
            return Results.Ok(shots);
        });

        group.MapGet("/courses", async (HttpContext ctx, IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            var courses = await roundService.GetPlayedCoursesByUserId(userId);
            return Results.Ok(courses);
        });

        return app;
    }

    private static async Task EnsureRoundAccess(IRoundService roundService, long roundId, string userId)
    {
        var round = await roundService.GetRoundByIdAsync(roundId);
        if (round is null)
            throw new NotFoundException("Round", roundId);

        var isOwner = await roundService.IsRoundOwnedByUserAsync(roundId, userId);
        if (!isOwner)
            throw new ForbiddenException("Round", roundId);
    }
}
