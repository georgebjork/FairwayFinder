using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Services.Admin;

namespace FairwayFinder.Admin.IntegrationTests.Services;

/// <summary>
/// The error log page's queries. The host's own startup can also write rows (anything it logs at
/// Warning+), so every assertion is scoped to this test's markers.
/// </summary>
public class AppLogServiceTests(AdminFactory factory) : AdminTestBase(factory)
{
    [Fact]
    public async Task Logs_are_paged_newest_first_and_search_is_case_insensitive()
    {
        var marker = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        await Data.CreateAppLogAsync($"{marker} Round save FAILED", timestamp: now.AddMinutes(-3), userEmail: "Golfer@Test.com");
        await Data.CreateAppLogAsync($"{marker} round save failed again", timestamp: now.AddMinutes(-2));
        await Data.CreateAppLogAsync($"{marker} stats slow", level: "Warning", timestamp: now.AddMinutes(-1));

        var service = Service<AppLogService>();

        var all = await service.GetLogsAsync(0, 10, search: marker);
        Assert.Equal(3, all.TotalCount);
        Assert.EndsWith("stats slow", all.Items[0].Message);

        var page = await service.GetLogsAsync(1, 1, search: marker);
        Assert.Equal(3, page.TotalCount);
        Assert.EndsWith("round save failed again", Assert.Single(page.Items).Message);

        Assert.Equal(2, (await service.GetLogsAsync(0, 10, search: $"{marker} ROUND SAVE")).TotalCount);
        Assert.Equal(1, (await service.GetLogsAsync(0, 10, search: "golfer@test.com")).TotalCount);
    }

    [Fact]
    public async Task Logs_filter_by_level_and_application()
    {
        var marker = Guid.NewGuid().ToString("N");
        await Data.CreateAppLogAsync($"{marker} a", level: "Warning", application: "FairwayFinder.Api");
        await Data.CreateAppLogAsync($"{marker} b", level: "Error", application: "FairwayFinder.Api");
        await Data.CreateAppLogAsync($"{marker} c", level: "Error", application: "FairwayFinder.Admin");

        var service = Service<AppLogService>();

        Assert.Equal(1, (await service.GetLogsAsync(0, 10, search: marker, level: "Warning")).TotalCount);
        Assert.Equal(2, (await service.GetLogsAsync(0, 10, search: marker, level: "Error")).TotalCount);
        Assert.Equal(1, (await service.GetLogsAsync(0, 10, search: marker, level: "Error", application: "FairwayFinder.Admin")).TotalCount);
        Assert.Contains("FairwayFinder.Admin", await service.GetApplicationsAsync());
    }

    [Fact]
    public async Task Detail_carries_the_stack_trace_the_list_leaves_out()
    {
        var entry = await Data.CreateAppLogAsync("detail please", userId: "u-1", userEmail: "golfer@test.com");

        var service = Service<AppLogService>();
        var listed = Assert.Single((await service.GetLogsAsync(0, 10, search: "detail please")).Items);
        var detail = await service.GetLogDetailAsync(entry.AppLogId);

        Assert.True(listed.HasException);
        Assert.NotNull(detail);
        Assert.Contains("InvalidOperationException: boom", detail.Exception);
        Assert.Equal("u-1", detail.UserId);
        Assert.Equal("golfer@test.com", detail.UserEmail);
        Assert.Equal(entry.TraceId, detail.TraceId);
        Assert.Contains("RoundId", detail.Properties);

        Assert.Null(await service.GetLogDetailAsync(-1));
    }

    [Fact]
    public async Task Purge_deletes_only_rows_past_retention()
    {
        var marker = Guid.NewGuid().ToString("N");
        await Data.CreateAppLogAsync($"{marker} old", timestamp: DateTime.UtcNow.AddDays(-45));
        await Data.CreateAppLogAsync($"{marker} new", timestamp: DateTime.UtcNow.AddDays(-1));

        var service = Service<AppLogService>();
        var removed = await service.PurgeOlderThanAsync(30);

        Assert.True(removed >= 1);
        Assert.EndsWith("new", Assert.Single((await service.GetLogsAsync(0, 10, search: marker)).Items).Message);
    }
}
