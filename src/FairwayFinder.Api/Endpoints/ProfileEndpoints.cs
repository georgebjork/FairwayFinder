using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Extensions;
using FairwayFinder.Api.Validators;
using FairwayFinder.Features.Data;
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

        // Change the golfer's username. Stored lowercase; a leading "@" is ignored.
        group.MapPut("/username", async (
            UpdateUsernameRequest request, HttpContext ctx, IUsernameService usernameService) =>
        {
            var userId = ctx.User.GetUserId();
            var result = await usernameService.ChangeUsernameAsync(userId, request.Username);

            return result.Status switch
            {
                UsernameChangeStatus.Ok => Results.Ok(new UsernameResponse(result.Username)),
                UsernameChangeStatus.Taken => Results.Problem(
                    detail: result.Error, statusCode: StatusCodes.Status409Conflict),
                UsernameChangeStatus.NotFound => throw new NotFoundException("User", userId),
                _ => Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest)
            };
        })
        .AddEndpointFilter<ValidationFilter<UpdateUsernameRequest>>();

        // Public stats page settings (fairwayfinder.pro/golf/{slug}). A blank slug keeps the current
        // one, or derives one from the username the first time the profile goes public.
        group.MapPut("/public", async (
            UpdatePublicProfileRequest request, HttpContext ctx, IProfileService profileService) =>
        {
            var userId = ctx.User.GetUserId();
            var result = await profileService.UpdatePublicProfileAsync(userId, new PublicProfileSettingsRequest
            {
                IsPublic = request.IsPublic,
                Slug = request.Slug,
                ShowStrokesGained = request.ShowStrokesGained,
                ShowRecentRounds = request.ShowRecentRounds,
                ShowCourseNames = request.ShowCourseNames
            }, actingUserId: userId);

            return result.Status switch
            {
                PublicProfileSettingsStatus.Ok => Results.Ok(await profileService.GetOrCreateProfileAsync(userId)),
                PublicProfileSettingsStatus.Taken => Results.Problem(
                    detail: result.Error, statusCode: StatusCodes.Status409Conflict),
                PublicProfileSettingsStatus.NotFound => throw new NotFoundException("User", userId),
                _ => Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest)
            };
        })
        .AddEndpointFilter<ValidationFilter<UpdatePublicProfileRequest>>();

        // Live check for the username editor: is this valid and free (your own counts as free)?
        group.MapGet("/username-available", async (
            string username, HttpContext ctx, IUsernameService usernameService) =>
        {
            var userId = ctx.User.GetUserId();
            return Results.Ok(await usernameService.CheckAvailabilityAsync(username, userId));
        });

        return app;
    }
}

/// <summary>Body for changing the user's username (e.g. { "username": "georgeb" }).</summary>
public record UpdateUsernameRequest(string Username);

public record UsernameResponse(string Username);

/// <summary>Body for updating the user's default strokes-gained golfer level (e.g. { "level": "Hcp10" }).</summary>
public record UpdateSgBaselineLevelRequest(BaselineLevel Level);

/// <summary>Body for the public stats page settings.</summary>
public record UpdatePublicProfileRequest(
    bool IsPublic,
    string? Slug,
    bool ShowStrokesGained = true,
    bool ShowRecentRounds = true,
    bool ShowCourseNames = true);
