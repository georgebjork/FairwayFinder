using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Services.Admin;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Admin.IntegrationTests.Services;

/// <summary>Dashboard metrics, device and game oversight, and the request log — the console's read and repair tools.</summary>
public class AdminOperationsServiceTests(AdminFactory factory) : AdminTestBase(factory)
{
    [Fact]
    public async Task Dashboard_counts_users_rounds_and_courses()
    {
        await Data.CreateAdminAsync();
        var active = await Data.CreateUserAsync();
        await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync();
        await Data.CreateCompletedRoundAsync(active.Id, course, datePlayed: DateOnly.FromDateTime(DateTime.UtcNow));
        await Data.CreateInvitationAsync("pending@test.fairwayfinder.pro");

        var metrics = await Service<AdminDashboardService>().GetDashboardMetricsAsync();

        Assert.Equal(3, metrics.Growth.TotalUsers);
        Assert.Equal(1, metrics.Growth.AdminCount);
        Assert.Equal(1, metrics.Growth.ActivatedUsers);
        Assert.Equal(1, metrics.Activity.TotalRounds);
        Assert.Equal(1, metrics.Activity.TotalCourses);
        Assert.Equal(1, metrics.Activity.RoundsLast7Days);
    }

    [Fact]
    public async Task Devices_list_shows_each_device_with_its_owner()
    {
        var golfer = await Data.CreateUserAsync(firstName: "Device", lastName: "Owner");
        await Service<IPushNotificationService>().RegisterDeviceAsync(golfer.Id, "admin-visible-token", "Phone");

        var devices = await Service<AdminDeviceService>().GetAllDevicesAsync();

        var device = Assert.Single(devices);
        Assert.Equal(golfer.Id, device.UserId);
        Assert.Equal(golfer.Email, device.PlayerEmail);
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task Games_can_be_inspected_reopened_and_deleted()
    {
        var admin = await Data.CreateAdminAsync();
        var host = await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync();
        var game = await Data.CreateGameAsync(host.Id, course);
        var service = Service<AdminGameService>();

        var listed = Assert.Single(await service.GetAllGamesAsync());
        Assert.Equal(game.GameId, listed.GameId);
        Assert.Equal(host.Id, listed.HostUserId);

        var detail = await service.GetGameForAdminAsync(game.GameId);
        Assert.NotNull(detail);
        Assert.Equal(game.GameId, detail.State.GameId);

        Assert.True(await service.SetGameStateAsync(game.GameId, GameState.Abandoned, admin.Id));
        Assert.Equal(GameState.Abandoned, (await service.GetGameForAdminAsync(game.GameId))!.State.State);

        Assert.True(await service.DeleteGameAsync(game.GameId, admin.Id));
        Assert.Empty(await service.GetAllGamesAsync());
        Assert.Null(await service.GetGameForAdminAsync(game.GameId));
    }

    [Fact]
    public async Task Recomputing_a_scoreboard_only_applies_to_posted_games()
    {
        var admin = await Data.CreateAdminAsync();
        var host = await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync();
        var game = await Data.CreateGameAsync(host.Id, course);

        Assert.False(await Service<AdminGameService>().RecomputeFinalScoreboardAsync(game.GameId, admin.Id));
    }

    [Fact]
    public async Task Request_log_search_is_case_insensitive_and_paged_newest_first()
    {
        var now = DateTime.UtcNow;
        await using (var db = Db())
        {
            db.ApiRequestLogs.AddRange(
                Log("/api/rounds", 200, now.AddMinutes(-3), "Golfer@Test.com"),
                Log("/api/Rounds/5", 404, now.AddMinutes(-2)),
                Log("/api/stats", 500, now.AddMinutes(-1)));
            await db.SaveChangesAsync();
        }

        var service = Service<ApiRequestLogService>();

        var rounds = await service.GetLogsAsync(0, 10, search: "ROUNDS");
        Assert.Equal(2, rounds.TotalCount);
        Assert.Equal("/api/Rounds/5", rounds.Items[0].Path);

        Assert.Equal(1, (await service.GetLogsAsync(0, 10, search: "golfer@test")).TotalCount);
        Assert.Equal(1, (await service.GetLogsAsync(0, 10, statusClass: 5)).TotalCount);
        Assert.Single((await service.GetLogsAsync(0, 1)).Items);
    }

    [Fact]
    public async Task Request_log_purge_deletes_only_rows_past_retention()
    {
        await using (var db = Db())
        {
            db.ApiRequestLogs.AddRange(
                Log("/old", 200, DateTime.UtcNow.AddDays(-45)),
                Log("/new", 200, DateTime.UtcNow.AddDays(-1)));
            await db.SaveChangesAsync();
        }

        var removed = await Service<ApiRequestLogService>().PurgeOlderThanAsync(30);

        Assert.Equal(1, removed);
        await using var check = Db();
        Assert.Equal("/new", (await check.ApiRequestLogs.SingleAsync()).Path);
    }

    private static ApiRequestLog Log(string path, int status, DateTime at, string? email = null) => new()
    {
        Timestamp = at,
        Method = "GET",
        Path = path,
        StatusCode = status,
        DurationMs = 5,
        UserEmail = email,
    };
}
