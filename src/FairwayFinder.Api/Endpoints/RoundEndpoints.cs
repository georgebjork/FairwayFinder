using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Extensions;
using FairwayFinder.Api.Validators;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Api.Endpoints;

public static class RoundEndpoints
{
    public static WebApplication MapRoundEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/rounds")
            .WithTags("Rounds")
            .RequireAuthorization();

        group.MapGet("/", async (
            long? courseId,
            bool? fullRoundOnly,
            DateOnly? startDate,
            DateOnly? endDate,
            HttpContext ctx,
            IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            var filter = new StatsFilter
            {
                CourseId = courseId,
                FullRoundOnly = fullRoundOnly,
                StartDate = startDate,
                EndDate = endDate
            };

            var rounds = filter.HasFilters
                ? await roundService.GetRoundsByUserIdAsync(userId, filter)
                : await roundService.GetRoundsByUserIdAsync(userId);
            return Results.Ok(rounds);
        });

        group.MapGet("/details", async (
            long? courseId,
            bool? fullRoundOnly,
            DateOnly? startDate,
            DateOnly? endDate,
            int? year,
            BaselineLevel? level,
            HttpContext ctx,
            IRoundService roundService,
            IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            var filter = new StatsFilter
            {
                CourseId = courseId,
                FullRoundOnly = fullRoundOnly,
                StartDate = startDate,
                EndDate = endDate,
                Year = year
            };

            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);
            var rounds = await roundService.GetRoundsWithDetailsAsync(
                userId, filter.HasFilters ? filter : null, effectiveLevel);
            return Results.Ok(rounds);
        });

        group.MapGet("/{roundId:long}", async (long roundId, BaselineLevel? level, HttpContext ctx, IRoundService roundService, IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            await EnsureRoundAccess(roundService, roundId, userId);

            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);
            var round = await roundService.GetRoundByIdAsync(roundId, effectiveLevel);
            if (round is null)
                throw new NotFoundException("Round", roundId);

            return Results.Ok(round);
        });

        // Submits a whole round in one call. Superseded by the start / upsert / complete flow
        // below, but kept working for iOS builds already in the field.
        group.MapPost("/", async (CreateRoundRequest request, HttpContext ctx, IRoundService roundService) =>
        {
            request.UserId = ctx.User.GetUserId();
            var newId = await roundService.CreateRoundAsync(request);
            return Results.Created($"/api/rounds/{newId}", newId);
        })
        .AddEndpointFilter<ValidationFilter<CreateRoundRequest>>()
        .WithSummary("[Deprecated] Submit a whole round at once. Use POST /rounds/start, " +
                     "PUT /rounds/{roundId}/holes/{holeNumber}, then POST /rounds/{roundId}/complete.");

        group.MapPut("/{roundId:long}", async (long roundId, UpdateRoundRequest request, HttpContext ctx, IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            request.UserId = userId;
            request.RoundId = roundId;

            await EnsureRoundAccess(roundService, roundId, userId);

            // This replaces the round's whole hole set, so it must not run against a round still
            // being entered — it would fight the per-hole writer over the same rows.
            var existing = await roundService.GetRoundByIdAsync(roundId);
            if (existing is not null && !existing.IsComplete)
                throw new ConflictException($"Round {roundId} is still in progress. Post it before editing it.");

            var success = await roundService.UpdateRoundAsync(request);
            return success ? Results.NoContent() : throw new NotFoundException("Round", roundId);
        }).AddEndpointFilter<ValidationFilter<UpdateRoundRequest>>();

        // ── Hole-by-hole entry ──
        // Scores reach the database as the golfer plays rather than in one submit at the end, so
        // a crash or a dead battery costs at most the hole in hand.

        group.MapPost("/start", async (
            StartRoundRequest request,
            HttpContext ctx,
            IRoundEntryService entryService) =>
        {
            var result = await entryService.StartRoundAsync(request, ctx.User.GetUserId());

            return result.Status switch
            {
                RoundEntryStatus.Ok => Results.Created($"/api/rounds/{result.Value!.RoundId}", result.Value),

                // Carries the open round's id so the app can offer resume-or-discard. Returned
                // directly rather than thrown because GlobalExceptionHandler forwards only the
                // message, and the id is the useful part.
                RoundEntryStatus.ActiveRoundExists => Results.Conflict(new
                {
                    error = "ActiveRoundExists",
                    message = "You already have a round in progress. Resume it or discard it before starting another.",
                    roundId = result.ConflictingRoundId
                }),

                RoundEntryStatus.TeeboxArchived => throw new ConflictException(
                    "Cannot start a round on an archived or unknown teebox."),

                _ => throw new NotFoundException("Teebox", request.TeeboxId)
            };
        }).AddEndpointFilter<ValidationFilter<StartRoundRequest>>();

        // The resume path: the only read that hands an unposted round back to its owner.
        group.MapGet("/active", async (
            BaselineLevel? level,
            HttpContext ctx,
            IRoundEntryService entryService,
            IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);

            var round = await entryService.GetActiveRoundAsync(userId, effectiveLevel);

            // 204 rather than 404: "have I got a round going?" is asked on every app launch, and
            // the common answer is not an error.
            return round is null ? Results.NoContent() : Results.Ok(round);
        });

        group.MapPut("/{roundId:long}/holes/{holeNumber:int:range(1,18)}", async (
            long roundId,
            int holeNumber,
            UpsertHoleRequest request,
            HttpContext ctx,
            IRoundEntryService entryService) =>
        {
            var result = await entryService.UpsertHoleAsync(roundId, holeNumber, request, ctx.User.GetUserId());
            return MapHoleResult(result, roundId, holeNumber);
        }).AddEndpointFilter<ValidationFilter<UpsertHoleRequest>>();

        group.MapDelete("/{roundId:long}/holes/{holeNumber:int:range(1,18)}", async (
            long roundId,
            int holeNumber,
            HttpContext ctx,
            IRoundEntryService entryService) =>
        {
            var result = await entryService.ClearHoleAsync(roundId, holeNumber, ctx.User.GetUserId());
            return MapHoleResult(result, roundId, holeNumber);
        });

        group.MapPost("/{roundId:long}/complete", async (
            long roundId,
            BaselineLevel? level,
            HttpContext ctx,
            IRoundEntryService entryService,
            IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);

            var result = await entryService.CompleteRoundAsync(roundId, userId, effectiveLevel);

            return result.Status switch
            {
                RoundEntryStatus.Ok => Results.Ok(result.Value),

                // Name the holes still needed so the app can walk the golfer to them.
                RoundEntryStatus.RoundIncomplete => Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["Holes"] =
                        [
                            result.MissingHoles.Count == 0
                                ? "Enter a score for every hole before posting the round."
                                : $"Missing a score for hole(s) {string.Join(", ", result.MissingHoles)}. " +
                                  "A round must cover a full 18, the front nine, or the back nine."
                        ]
                    },
                    detail: "Round is not ready to post."),

                RoundEntryStatus.NotOwner => throw new ForbiddenException("Round", roundId),
                _ => throw new NotFoundException("Round", roundId)
            };
        });

        // Separate from the PUT because that takes a whole round (teebox, date, every hole) and
        // cannot express a lone flag flip.
        group.MapPatch("/{roundId:long}/exclude-from-stats", async (
            long roundId,
            SetExcludeFromStatsRequest request,
            HttpContext ctx,
            IRoundService roundService) =>
        {
            var userId = ctx.User.GetUserId();
            await EnsureRoundAccess(roundService, roundId, userId);

            var success = await roundService.SetExcludeFromStatsAsync(roundId, request.ExcludeFromStats, userId);
            return success ? Results.NoContent() : throw new NotFoundException("Round", roundId);
        });

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

    // Uses the explicit ?level= when supplied, otherwise the user's saved default (SgBaselineLevel).
    private static async Task<BaselineLevel> ResolveLevelAsync(BaselineLevel? level, string userId, IProfileService profileService)
        => level ?? (await profileService.GetOrCreateProfileAsync(userId)).SgBaselineLevel;

    // The per-hole endpoints share one status-to-HTTP mapping so a write and a clear fail the
    // same way for the same reason.
    private static IResult MapHoleResult(RoundEntryResult<RoundProgressResponse> result, long roundId, int holeNumber)
        => result.Status switch
        {
            RoundEntryStatus.Ok => Results.Ok(result.Value),
            RoundEntryStatus.NotOwner => throw new ForbiddenException("Round", roundId),
            RoundEntryStatus.HoleNotOnTeebox => throw new NotFoundException("Hole", holeNumber),
            RoundEntryStatus.RoundAlreadyComplete => throw new ConflictException(
                $"Round {roundId} has already been posted. Edit it with PUT /api/rounds/{roundId}."),
            _ => throw new NotFoundException("Round", roundId)
        };

    private static async Task EnsureRoundAccess(IRoundService roundService, long roundId, string userId)
    {
        // One lightweight projection query answers both existence (404) and ownership (403) —
        // no need to materialize the whole round just to guard access.
        var ownerId = await roundService.GetRoundOwnerIdAsync(roundId);
        if (ownerId is null)
            throw new NotFoundException("Round", roundId);

        if (ownerId != userId)
            throw new ForbiddenException("Round", roundId);
    }
}
