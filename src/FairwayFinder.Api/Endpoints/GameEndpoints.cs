using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Extensions;
using FairwayFinder.Api.Validators;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.Net.Http.Headers;

namespace FairwayFinder.Api.Endpoints;

public static class GameEndpoints
{
    public static WebApplication MapGameEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games")
            .WithTags("Games")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreateGameRequest request,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.CreateGameAsync(request, ctx.User.GetUserId());

            return result.IsOk
                ? Results.Created($"/api/games/{result.Value!.GameId}", result.Value)
                : MapGameResult(result, gameId: 0);
        }).AddEndpointFilter<ValidationFilter<CreateGameRequest>>();

        group.MapGet("/", async (
            bool? activeOnly,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var games = await gameService.GetMyGamesAsync(ctx.User.GetUserId(), activeOnly ?? true);
            return Results.Ok(games);
        });

        // The poll endpoint. A phone asks this every few seconds while a match is live, so it
        // answers 304 whenever nothing that feeds the scoreboard has moved.
        group.MapGet("/{gameId:long}", async (
            long gameId,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.GetGameAsync(gameId, ctx.User.GetUserId());
            if (!result.IsOk) return MapGameResult(result, gameId);

            return WithETag(ctx, result.Value!);
        });

        group.MapPost("/join", async (
            JoinGameRequest request,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.JoinGameAsync(request, ctx.User.GetUserId());
            return MapGameResult(result, gameId: 0);
        }).AddEndpointFilter<ValidationFilter<JoinGameRequest>>();

        group.MapPost("/{gameId:long}/participants", async (
            long gameId,
            AddParticipantRequest request,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.AddParticipantAsync(gameId, request, ctx.User.GetUserId());
            return MapGameResult(result, gameId);
        }).AddEndpointFilter<ValidationFilter<AddParticipantRequest>>();

        group.MapPut("/{gameId:long}/participants/{participantId:long}", async (
            long gameId,
            long participantId,
            UpdateParticipantRequest request,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.UpdateParticipantAsync(gameId, participantId, request, ctx.User.GetUserId());
            return MapGameResult(result, gameId, participantId);
        });

        group.MapDelete("/{gameId:long}/participants/{participantId:long}", async (
            long gameId,
            long participantId,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.RemoveParticipantAsync(gameId, participantId, ctx.User.GetUserId());
            return MapGameResult(result, gameId, participantId);
        });

        // Link my own in-progress round, or unlink it by sending a null round id.
        group.MapPost("/{gameId:long}/round", async (
            long gameId,
            LinkRoundRequest request,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.LinkRoundAsync(gameId, request.RoundId, ctx.User.GetUserId());
            return MapGameResult(result, gameId);
        });

        group.MapPut("/{gameId:long}/participants/{participantId:long}/holes/{holeNumber:int:range(1,18)}", async (
            long gameId,
            long participantId,
            int holeNumber,
            UpsertGameHoleRequest request,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.UpsertParticipantHoleAsync(
                gameId, participantId, holeNumber, request, ctx.User.GetUserId());

            return MapGameResult(result, gameId, participantId, holeNumber);
        }).AddEndpointFilter<ValidationFilter<UpsertGameHoleRequest>>();

        group.MapDelete("/{gameId:long}/participants/{participantId:long}/holes/{holeNumber:int:range(1,18)}", async (
            long gameId,
            long participantId,
            int holeNumber,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.ClearParticipantHoleAsync(
                gameId, participantId, holeNumber, ctx.User.GetUserId());

            return MapGameResult(result, gameId, participantId, holeNumber);
        });

        group.MapPost("/{gameId:long}/start", async (
            long gameId,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.StartGameAsync(gameId, ctx.User.GetUserId());
            return MapGameResult(result, gameId);
        });

        group.MapPost("/{gameId:long}/complete", async (
            long gameId,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.CompleteGameAsync(gameId, ctx.User.GetUserId());
            return MapGameResult(result, gameId);
        });

        group.MapPost("/{gameId:long}/abandon", async (
            long gameId,
            HttpContext ctx,
            IGameService gameService) =>
        {
            var result = await gameService.AbandonGameAsync(gameId, ctx.User.GetUserId());
            return MapGameResult(result, gameId);
        });

        return app;
    }

    /// <summary>
    /// Answers the poll with a weak ETag over the whole serialized response, honouring
    /// <c>If-None-Match</c> with a 304.
    ///
    /// Hashing the response rather than just the strokes is deliberate: a host changing someone's
    /// course handicap, their tees, or the game's rules moves every net score without touching a
    /// single stroke, and a narrower hash would leave a polling phone showing a stale result
    /// indefinitely. This saves bandwidth, not database work — the reader and engine have already
    /// run by the time we get here.
    /// </summary>
    private static IResult WithETag(HttpContext ctx, GameStateResponse state)
    {
        var payload = JsonSerializer.Serialize(state);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..32];
        var etag = $"W/\"{hash}\"";

        var requested = ctx.Request.Headers[HeaderNames.IfNoneMatch].ToString();
        if (!string.IsNullOrEmpty(requested) && requested.Split(',').Any(t => t.Trim() == etag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        ctx.Response.Headers[HeaderNames.ETag] = etag;
        return Results.Ok(state);
    }

    /// <summary>
    /// One status-to-HTTP mapping for every game endpoint, so the same refusal fails the same way
    /// wherever it happens. Mirrors <c>RoundEndpoints.MapHoleResult</c>.
    /// </summary>
    private static IResult MapGameResult<T>(
        GameResult<T> result, long gameId, long? participantId = null, int? holeNumber = null)
        => result.Status switch
        {
            GameResultStatus.Ok => Results.Ok(result.Value),

            // A non-participant gets 404, not 403 — the same reason FriendEndpoints does it:
            // a 403 would confirm the game exists.
            GameResultStatus.NotParticipant or GameResultStatus.GameNotFound
                => throw new NotFoundException("Game", gameId),
            GameResultStatus.JoinCodeInvalid
                => throw new NotFoundException("Game", "that join code"),

            GameResultStatus.ParticipantNotFound
                => throw new NotFoundException("GameParticipant", participantId ?? gameId),
            GameResultStatus.HoleNotInGame
                => throw new NotFoundException("Hole", holeNumber ?? 0),

            GameResultStatus.NotHost => throw new ForbiddenException("Game", gameId),

            // Detail carries the useful half of these — which holes a teebox is missing, how many
            // sides the field actually has — and ConflictException's message is forwarded intact.
            GameResultStatus.GameNotInSetup or GameResultStatus.GameNotActive
                or GameResultStatus.GameAlreadyComplete or GameResultStatus.AlreadyJoined
                or GameResultStatus.RoundNotOwned or GameResultStatus.RoundNotOnGameCourse
                or GameResultStatus.RoundNotActive or GameResultStatus.ParticipantCountInvalid
                or GameResultStatus.GameShapeUnsupported or GameResultStatus.TeeboxArchived
                or GameResultStatus.TeeboxNotOnCourse or GameResultStatus.TeeboxShapeMismatch
                or GameResultStatus.ParticipantUsesLinkedRound or GameResultStatus.NotFriends
                or GameResultStatus.GameTypeNotSupported
                => throw new ConflictException(result.Detail ?? $"Game {gameId} cannot accept that right now."),

            _ => throw new NotFoundException("Game", gameId)
        };
}
