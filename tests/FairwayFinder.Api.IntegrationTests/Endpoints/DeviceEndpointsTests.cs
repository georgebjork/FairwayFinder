using System.Net;
using System.Net.Http.Json;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

public class DeviceEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Registering_a_device_stores_it_once_and_re_registering_updates_it()
    {
        var golfer = await SignInNewUserAsync();

        await RegisterDeviceAsync(golfer, "token-abc");
        await RegisterDeviceAsync(golfer, "token-abc");

        await using var db = Db();
        var device = await db.UserDevices.SingleAsync(d => d.DeviceToken == "token-abc");
        Assert.Equal(golfer.Id, device.UserId);
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task A_token_moving_to_another_account_follows_the_new_owner()
    {
        var first = await SignInNewUserAsync();
        var second = await SignInNewUserAsync();

        await RegisterDeviceAsync(first, "shared-phone");
        await RegisterDeviceAsync(second, "shared-phone");

        await using var db = Db();
        Assert.Equal(second.Id, (await db.UserDevices.SingleAsync(d => d.DeviceToken == "shared-phone")).UserId);
    }

    [Fact]
    public async Task Unregistering_deactivates_the_device()
    {
        var golfer = await SignInNewUserAsync();
        await RegisterDeviceAsync(golfer, "token-gone");

        await AssertStatusAsync(HttpStatusCode.NoContent, await golfer.Client.DeleteAsync("/api/devices/token-gone"));

        await using var db = Db();
        Assert.False((await db.UserDevices.SingleAsync(d => d.DeviceToken == "token-gone")).IsActive);
    }

    [Fact]
    public async Task Empty_device_token_is_a_validation_error()
    {
        var golfer = await SignInNewUserAsync();

        var response = await golfer.Client.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest { DeviceToken = "" });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task Test_push_is_admin_only_and_reaches_the_targets_devices()
    {
        var admin = await SignInNewAdminAsync();
        var golfer = await SignInNewUserAsync();
        await RegisterDeviceAsync(golfer);
        var request = new SendTestPushRequest { TargetUserId = golfer.Id, Title = "Hello", Body = "From the tests" };

        await AssertStatusAsync(HttpStatusCode.Forbidden, await golfer.Client.PostAsJsonAsync("/api/devices/test", request));

        var result = await ReadAsync<SendTestPushResponse>(await admin.Client.PostAsJsonAsync("/api/devices/test", request));
        Assert.Equal(1, result.Sent);
        Assert.Single(Apns.Sent);
    }
}
