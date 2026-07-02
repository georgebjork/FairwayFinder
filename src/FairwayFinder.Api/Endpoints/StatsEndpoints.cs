using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Extensions;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Api.Endpoints;

public static class StatsEndpoints
{
    public static WebApplication MapStatsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stats")
            .WithTags("Stats")
            .RequireAuthorization();

        group.MapGet("/", async (
            bool? fullRoundOnly,
            DateOnly? startDate,
            DateOnly? endDate,
            long? courseId,
            int? year,
            BaselineLevel? level,
            HttpContext ctx,
            IStatsService statsService,
            IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();

            var filter = new StatsFilter
            {
                FullRoundOnly = fullRoundOnly,
                StartDate = startDate,
                EndDate = endDate,
                CourseId = courseId,
                Year = year
            };

            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);
            var stats = await statsService.GetUserStatsAsync(userId, filter.HasFilters ? filter : null, level: effectiveLevel);
            return Results.Ok(stats);
        });

        group.MapGet("/courses/{courseId:long}", async (
            long courseId,
            long? teeboxId,
            DateOnly? startDate,
            DateOnly? endDate,
            bool? fullRoundOnly,
            int? year,
            BaselineLevel? level,
            HttpContext ctx,
            IStatsService statsService,
            IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);
            var stats = await statsService.GetCourseStatsAsync(userId, courseId, teeboxId, startDate, endDate, fullRoundOnly, year, effectiveLevel);
            if (stats is null)
                throw new NotFoundException("CourseStats", courseId);

            return Results.Ok(stats);
        });

        group.MapGet("/courses/{courseId:long}/holes", async (
            long courseId,
            long? teeboxId,
            DateOnly? startDate,
            DateOnly? endDate,
            bool? fullRoundOnly,
            int? year,
            BaselineLevel? level,
            HttpContext ctx,
            IStatsService statsService,
            IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);
            var stats = await statsService.GetCourseHoleStatsAsync(userId, courseId, teeboxId, startDate, endDate, fullRoundOnly, year, effectiveLevel);
            if (stats is null)
                throw new NotFoundException("CourseHoleStats", courseId);

            return Results.Ok(stats);
        });

        group.MapGet("/years", async (HttpContext ctx, IStatsService statsService) =>
        {
            var userId = ctx.User.GetUserId();
            var years = await statsService.GetAvailableYearsAsync(userId);
            return Results.Ok(years);
        });

        group.MapGet("/courses", async (HttpContext ctx, IStatsService statsService) =>
        {
            var userId = ctx.User.GetUserId();
            var courses = await statsService.GetUserCoursesAsync(userId);
            return Results.Ok(courses);
        });

        return app;
    }

    // Uses the explicit ?level= when supplied, otherwise the user's saved default (SgBaselineLevel).
    private static async Task<BaselineLevel> ResolveLevelAsync(BaselineLevel? level, string userId, IProfileService profileService)
        => level ?? (await profileService.GetOrCreateProfileAsync(userId)).SgBaselineLevel;
}
