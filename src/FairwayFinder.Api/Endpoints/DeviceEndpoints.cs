using FairwayFinder.Api.Extensions;
using FairwayFinder.Api.Validators;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Api.Endpoints;

public static class DeviceEndpoints
{
    public static WebApplication MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/devices")
            .WithTags("Devices")
            .RequireAuthorization();

        group.MapPost("/", async (RegisterDeviceRequest request, HttpContext ctx, IPushNotificationService pushService) =>
        {
            var userId = ctx.User.GetUserId();
            await pushService.RegisterDeviceAsync(userId, request.DeviceToken, request.DeviceName);
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<RegisterDeviceRequest>>();

        group.MapDelete("/{token}", async (string token, IPushNotificationService pushService) =>
        {
            await pushService.UnregisterDeviceAsync(token);
            return Results.NoContent();
        });

        group.MapPost("/test", async (SendTestPushRequest request, IPushNotificationService pushService) =>
        {
            var sent = await pushService.SendToUserAsync(request.TargetUserId, request.Title, request.Body, request.Badge);
            return Results.Ok(new SendTestPushResponse { Sent = sent });
        })
        .RequireAuthorization(p => p.RequireRole("Admin"))
        .AddEndpointFilter<ValidationFilter<SendTestPushRequest>>();

        return app;
    }
}
