using FairwayFinder.Api.Extensions;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Api.Endpoints;

public static class ProfileEndpoints
{
    public static WebApplication MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        group.MapGet("/", async (HttpContext ctx, IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            var profile = await profileService.GetOrCreateProfileAsync(userId);
            return Results.Ok(profile);
        });

        // Update the default golfer level used to compute strokes gained.
        group.MapPut("/sg-baseline-level", async (
            UpdateSgBaselineLevelRequest request, HttpContext ctx, IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            await profileService.UpdateSgBaselineLevelAsync(userId, request.Level);
            return Results.NoContent();
        });

        return app;
    }
}

/// <summary>Body for updating the user's default strokes-gained golfer level (e.g. { "level": "Hcp10" }).</summary>
public record UpdateSgBaselineLevelRequest(BaselineLevel Level);
