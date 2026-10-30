using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Extensions;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Services.Analytics;
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
            IProfileService profileService,
            IProductAnalytics analytics) =>
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
            CaptureStatsViewed(analytics, userId, AnalyticsEvents.StatsScopes.Overall, effectiveLevel, filter.HasFilters);
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
            IProfileService profileService,
            IProductAnalytics analytics) =>
        {
            var userId = ctx.User.GetUserId();
            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);
            var stats = await statsService.GetCourseStatsAsync(userId, courseId, teeboxId, startDate, endDate, fullRoundOnly, year, effectiveLevel);
            if (stats is null)
                throw new NotFoundException("CourseStats", courseId);

            CaptureStatsViewed(analytics, userId, AnalyticsEvents.StatsScopes.Course, effectiveLevel,
                teeboxId is not null || startDate is not null || endDate is not null || fullRoundOnly is not null || year is not null,
                courseId);
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
            IProfileService profileService,
            IProductAnalytics analytics) =>
        {
            var userId = ctx.User.GetUserId();
            var effectiveLevel = await ResolveLevelAsync(level, userId, profileService);
            var stats = await statsService.GetCourseHoleStatsAsync(userId, courseId, teeboxId, startDate, endDate, fullRoundOnly, year, effectiveLevel);
            if (stats is null)
                throw new NotFoundException("CourseHoleStats", courseId);

            CaptureStatsViewed(analytics, userId, AnalyticsEvents.StatsScopes.CourseHoles, effectiveLevel,
                teeboxId is not null || startDate is not null || endDate is not null || fullRoundOnly is not null || year is not null,
                courseId);
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

    /// <summary>
    /// Sent here rather than from <c>StatsService</c>, which the admin console and the friend
    /// endpoints also call for someone else's stats: only here is the viewer the stats' owner.
    /// </summary>
    private static void CaptureStatsViewed(
        IProductAnalytics analytics, string userId, string scope, BaselineLevel level, bool filtered, long? courseId = null)
        => analytics.Capture(userId, AnalyticsEvents.StatsViewed, new Dictionary<string, object?>
        {
            [AnalyticsEvents.Props.Scope] = scope,
            [AnalyticsEvents.Props.BaselineLevel] = level.ToString(),
            [AnalyticsEvents.Props.Filtered] = filtered,
            [AnalyticsEvents.Props.CourseId] = courseId
        });
}
