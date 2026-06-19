using FairwayFinder.Api.Extensions;
using FairwayFinder.Api.Validators;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;

namespace FairwayFinder.Api.Endpoints;

public static class AdminInviteEndpoints
{
    public static WebApplication MapAdminInviteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/invites")
            .WithTags("Admin Invites")
            .RequireAuthorization(p => p.RequireRole(ApplicationRoles.Admin));

        group.MapGet("/", async (IUserInvitationService inviteService) =>
        {
            var invites = await inviteService.GetPendingInvitesAsync();
            return Results.Ok(invites);
        });

        group.MapPost("/", async (
            SendInviteRequest request,
            HttpContext ctx,
            IUserInvitationService inviteService) =>
        {
            var userId = ctx.User.GetUserId();
            var result = await inviteService.CreateAndSendInviteAsync(request.Email, userId);

            if (!result.Success)
                return Results.Problem(
                    detail: result.Error ?? "Failed to send invitation.",
                    statusCode: StatusCodes.Status400BadRequest);

            return Results.NoContent();
        })
        .AddEndpointFilter<ValidationFilter<SendInviteRequest>>();

        group.MapDelete("/{id:int}", async (
            int id,
            HttpContext ctx,
            IUserInvitationService inviteService) =>
        {
            var userId = ctx.User.GetUserId();
            var revoked = await inviteService.RevokeInviteAsync(id, userId);
            return revoked ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
