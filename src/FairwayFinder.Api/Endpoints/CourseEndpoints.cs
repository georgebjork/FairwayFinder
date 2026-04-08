using FairwayFinder.Api.Extensions;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Api.Endpoints;

public static class CourseEndpoints
{
    public static WebApplication MapCourseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/courses")
            .WithTags("Courses")
            .RequireAuthorization();

        group.MapGet("/search", async (string query, ICourseService courseService) =>
        {
            var results = await courseService.SearchCoursesAsync(query);
            return Results.Ok(results);
        });

        group.MapGet("/{courseId:long}/teeboxes", async (long courseId, ICourseService courseService) =>
        {
            var teeboxes = await courseService.GetTeeboxesAsync(courseId);
            return Results.Ok(teeboxes);
        });

        group.MapGet("/teeboxes/{teeboxId:long}/holes", async (long teeboxId, ICourseService courseService) =>
        {
            var holes = await courseService.GetHolesAsync(teeboxId);
            return Results.Ok(holes);
        });

        return app;
    }
}
