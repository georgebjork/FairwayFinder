using FairwayFinder.Api.Extensions;
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

        return app;
    }
}
