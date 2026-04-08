using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Extensions;
using FairwayFinder.Features.Data;
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
            HttpContext ctx,
            IStatsService statsService) =>
        {
            var userId = ctx.User.GetUserId();

            var filter = new StatsFilter
            {
                FullRoundOnly = fullRoundOnly,
                StartDate = startDate,
                EndDate = endDate,
                CourseId = courseId
            };

            var stats = await statsService.GetUserStatsAsync(userId, filter.HasFilters ? filter : null);
            return Results.Ok(stats);
        });

        group.MapGet("/courses/{courseId:long}", async (
            long courseId,
            long? teeboxId,
            DateOnly? startDate,
            DateOnly? endDate,
            HttpContext ctx,
            IStatsService statsService) =>
        {
            var userId = ctx.User.GetUserId();
            var stats = await statsService.GetCourseStatsAsync(userId, courseId, teeboxId, startDate, endDate);
            if (stats is null)
                throw new NotFoundException("CourseStats", courseId);

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
}
