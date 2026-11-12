using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.RateLimiting;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Api.Endpoints;

/// <summary>
/// The only anonymous data the API serves: a golfer's opt-in public stats page. Its consumer is the
/// Landing site's build, which bakes each page into <c>fairwayfinder.pro/golf/{slug}</c>.
/// </summary>
public static class PublicProfileEndpoints
{
    public static WebApplication MapPublicProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/public")
            .WithTags("Public")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.PublicProfilePolicy);

        // 404 for an unknown slug and a private profile alike, so nobody can probe which exist.
        group.MapGet("/profiles/{slug}", async (string slug, HttpContext ctx, IPublicProfileService publicProfiles) =>
        {
            var profile = await publicProfiles.GetBySlugAsync(slug)
                          ?? throw new NotFoundException("PublicProfile", slug);

            ctx.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(profile);
        });

        return app;
    }
}
