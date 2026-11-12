using System.Net;
using System.Net.Http.Json;
using FairwayFinder.Api.Endpoints;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

public class ProfileAndLookupEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Profile_describes_the_signed_in_golfer()
    {
        var golfer = await SignInNewUserAsync();

        var profile = await GetAsync<UserProfileResponse>(golfer.Client, "/api/profile");

        Assert.Equal(golfer.Id, profile.UserId);
        Assert.NotEqual(Guid.Empty, profile.PublicIdentifier);
        Assert.Equal(BaselineLevel.Scratch, profile.SgBaselineLevel);
    }

    [Fact]
    public async Task Strokes_gained_level_can_be_changed()
    {
        var golfer = await SignInNewUserAsync();

        await AssertStatusAsync(HttpStatusCode.NoContent, await golfer.Client.PutAsJsonAsync(
            "/api/profile/sg-baseline-level", new UpdateSgBaselineLevelRequest(BaselineLevel.Hcp15), Json));

        var profile = await GetAsync<UserProfileResponse>(golfer.Client, "/api/profile");
        Assert.Equal(BaselineLevel.Hcp15, profile.SgBaselineLevel);
    }

    [Fact]
    public async Task Username_can_be_changed_and_is_stored_lowercase()
    {
        var golfer = await SignInNewUserAsync();

        var changed = await ReadAsync<UsernameResponse>(await golfer.Client.PutAsJsonAsync(
            "/api/profile/username", new UpdateUsernameRequest("@Tee.Time_42"), Json));

        Assert.Equal("tee.time_42", changed.Username);
        Assert.Equal("tee.time_42", (await GetAsync<UserProfileResponse>(golfer.Client, "/api/profile")).Username);

        // Setting it to what it already is is a no-op, not a conflict with yourself.
        await AssertStatusAsync(HttpStatusCode.OK, await golfer.Client.PutAsJsonAsync(
            "/api/profile/username", new UpdateUsernameRequest("TEE.TIME_42"), Json));
    }

    [Fact]
    public async Task Username_taken_by_someone_else_in_any_case_conflicts()
    {
        await Data.CreateUserAsync(username: "fairwaysfirst");
        var golfer = await SignInNewUserAsync();

        await AssertStatusAsync(HttpStatusCode.Conflict, await golfer.Client.PutAsJsonAsync(
            "/api/profile/username", new UpdateUsernameRequest("FairwaysFirst"), Json));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("tiger..woods")]
    [InlineData("tiger woods")]
    [InlineData("admin")]
    [InlineData("")]
    public async Task Invalid_or_reserved_usernames_are_rejected(string username)
    {
        var golfer = await SignInNewUserAsync();
        var before = (await GetAsync<UserProfileResponse>(golfer.Client, "/api/profile")).Username;

        await AssertStatusAsync(HttpStatusCode.BadRequest, await golfer.Client.PutAsJsonAsync(
            "/api/profile/username", new UpdateUsernameRequest(username), Json));

        Assert.Equal(before, (await GetAsync<UserProfileResponse>(golfer.Client, "/api/profile")).Username);
    }

    [Fact]
    public async Task Username_availability_reports_free_taken_invalid_and_your_own()
    {
        await Data.CreateUserAsync(username: "takenname");
        var golfer = await SignInNewUserAsync();
        var mine = (await GetAsync<UserProfileResponse>(golfer.Client, "/api/profile")).Username;

        var free = await GetAsync<UsernameAvailability>(golfer.Client, "/api/profile/username-available?username=FreeName");
        var taken = await GetAsync<UsernameAvailability>(golfer.Client, "/api/profile/username-available?username=takenname");
        var invalid = await GetAsync<UsernameAvailability>(golfer.Client, "/api/profile/username-available?username=a");
        var own = await GetAsync<UsernameAvailability>(golfer.Client, $"/api/profile/username-available?username={mine}");

        Assert.True(free.Available);
        Assert.Equal("freename", free.Username);
        Assert.False(taken.Available);
        Assert.False(invalid.Available);
        Assert.NotNull(invalid.Reason);
        Assert.True(own.Available);
    }

    [Fact]
    public async Task Miss_types_come_from_the_migration_seeded_lookup_table()
    {
        var golfer = await SignInNewUserAsync();

        var missTypes = await GetJsonAsync(golfer.Client, "/api/lookups/miss-types");

        Assert.True(missTypes.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Lie_types_and_distance_units_are_listed()
    {
        var golfer = await SignInNewUserAsync();

        var lies = await GetJsonAsync(golfer.Client, "/api/lookups/lie-types");
        var units = await GetJsonAsync(golfer.Client, "/api/lookups/distance-units");

        Assert.Equal(Enum.GetValues<LieType>().Length, lies.GetArrayLength());
        Assert.Equal(2, units.GetArrayLength());
    }
}
