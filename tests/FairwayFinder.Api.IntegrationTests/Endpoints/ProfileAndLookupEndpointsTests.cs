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
