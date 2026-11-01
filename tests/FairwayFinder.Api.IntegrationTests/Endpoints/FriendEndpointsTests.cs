using System.Net;
using System.Net.Http.Json;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using static FairwayFinder.Api.IntegrationTests.Infrastructure.RoundFlows;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

/// <summary>
/// Search, the request lifecycle, and the privacy boundary: a golfer's rounds and stats are
/// visible to their friends and to nobody else.
/// </summary>
public class FriendEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Search_matches_first_last_and_full_name_case_insensitively_and_excludes_self()
    {
        var me = await SignInNewUserAsync(firstName: "Searcher");
        var tiger = await Data.CreateUserAsync(firstName: "Tiger", lastName: "Woodstest");
        await Data.CreateUserAsync(firstName: "Phil", lastName: "Mickeltest");

        var byFirst = await GetAsync<List<UserSearchResultResponse>>(me.Client, "/api/friends/search?query=tIgEr");
        var byFull = await GetAsync<List<UserSearchResultResponse>>(me.Client, "/api/friends/search?query=tiger%20woods");
        var self = await GetAsync<List<UserSearchResultResponse>>(me.Client, "/api/friends/search?query=Searcher");

        Assert.Equal(tiger.Id, Assert.Single(byFirst).UserId);
        Assert.Equal(tiger.Id, Assert.Single(byFull).UserId);
        Assert.Empty(self);
    }

    [Fact]
    public async Task Search_needs_at_least_two_characters()
    {
        var me = await SignInNewUserAsync();
        await AssertStatusAsync(HttpStatusCode.BadRequest, await me.Client.GetAsync("/api/friends/search?query=a"));
    }

    [Fact]
    public async Task Request_accept_lifecycle_makes_both_golfers_friends_and_notifies_each()
    {
        var alice = await SignInNewUserAsync(firstName: "Alice");
        var bob = await SignInNewUserAsync(firstName: "Bob");
        await RegisterDeviceAsync(alice);
        await RegisterDeviceAsync(bob);

        var sent = await alice.Client.PostAsJsonAsync("/api/friends/requests",
            new SendFriendRequestRequest { AddresseeUserId = bob.Id });
        var friendshipId = await ReadAsync<long>(sent, HttpStatusCode.Created);

        Assert.Equal(1, await GetAsync<int>(bob.Client, "/api/friends/requests/incoming/count"));
        Assert.Single(await GetAsync<List<FriendRequestResponse>>(bob.Client, "/api/friends/requests/incoming"));
        Assert.Single(await GetAsync<List<FriendRequestResponse>>(alice.Client, "/api/friends/requests/outgoing"));

        var search = await GetAsync<List<UserSearchResultResponse>>(alice.Client, "/api/friends/search?query=Bob");
        Assert.Equal(FriendshipState.PendingOutgoing, Assert.Single(search).FriendshipState);

        await AssertStatusAsync(HttpStatusCode.NoContent,
            await bob.Client.PutAsync($"/api/friends/requests/{friendshipId}/accept", null));

        Assert.Equal(bob.Id, Assert.Single(await GetAsync<List<FriendResponse>>(alice.Client, "/api/friends")).UserId);
        Assert.Equal(alice.Id, Assert.Single(await GetAsync<List<FriendResponse>>(bob.Client, "/api/friends")).UserId);

        // One push for the request (to Bob), one for the acceptance (to Alice).
        Assert.Equal(2, Apns.Sent.Count);
    }

    [Fact]
    public async Task Duplicate_request_conflicts()
    {
        var alice = await SignInNewUserAsync();
        var bob = await SignInNewUserAsync();
        var request = new SendFriendRequestRequest { AddresseeUserId = bob.Id };

        await ReadAsync<long>(await alice.Client.PostAsJsonAsync("/api/friends/requests", request), HttpStatusCode.Created);
        var again = await alice.Client.PostAsJsonAsync("/api/friends/requests", request);

        await AssertStatusAsync(HttpStatusCode.Conflict, again);
    }

    [Fact]
    public async Task Only_the_addressee_can_accept_and_rejected_or_cancelled_requests_disappear()
    {
        var alice = await SignInNewUserAsync();
        var bob = await SignInNewUserAsync();
        var carol = await SignInNewUserAsync();

        var toBob = await ReadAsync<long>(await alice.Client.PostAsJsonAsync("/api/friends/requests",
            new SendFriendRequestRequest { AddresseeUserId = bob.Id }), HttpStatusCode.Created);
        var toCarol = await ReadAsync<long>(await alice.Client.PostAsJsonAsync("/api/friends/requests",
            new SendFriendRequestRequest { AddresseeUserId = carol.Id }), HttpStatusCode.Created);

        // The requester cannot accept on the addressee's behalf.
        await AssertStatusAsync(HttpStatusCode.NotFound, await alice.Client.PutAsync($"/api/friends/requests/{toBob}/accept", null));

        await AssertStatusAsync(HttpStatusCode.NoContent, await bob.Client.PutAsync($"/api/friends/requests/{toBob}/reject", null));
        await AssertStatusAsync(HttpStatusCode.NoContent, await alice.Client.DeleteAsync($"/api/friends/requests/{toCarol}"));

        Assert.Empty(await GetAsync<List<FriendRequestResponse>>(alice.Client, "/api/friends/requests/outgoing"));
        Assert.Empty(await GetAsync<List<FriendResponse>>(alice.Client, "/api/friends"));
        Assert.Equal(0, await GetAsync<int>(bob.Client, "/api/friends/requests/incoming/count"));
    }

    [Fact]
    public async Task Removing_a_friend_ends_the_friendship_for_both()
    {
        var alice = await SignInNewUserAsync();
        var bob = await SignInNewUserAsync();
        var friendshipId = await MakeFriendsAsync(alice, bob);

        await AssertStatusAsync(HttpStatusCode.NoContent, await bob.Client.DeleteAsync($"/api/friends/{friendshipId}"));

        Assert.Empty(await GetAsync<List<FriendResponse>>(alice.Client, "/api/friends"));
        Assert.Empty(await GetAsync<List<FriendResponse>>(bob.Client, "/api/friends"));
    }

    [Fact]
    public async Task Friends_can_see_each_others_posted_rounds_and_stats()
    {
        var alice = await SignInNewUserAsync();
        var bob = await SignInNewUserAsync();
        await MakeFriendsAsync(alice, bob);
        var course = await Data.CreateCourseAsync();
        var roundId = await PlayCompletedRoundAsync(bob.Client, course);
        var bobPublicId = (await GetAsync<UserProfileResponse>(bob.Client, "/api/profile")).PublicIdentifier;

        var rounds = await GetAsync<List<RoundResponse>>(alice.Client, $"/api/friends/{bobPublicId}/rounds");
        var round = await GetAsync<RoundResponse>(alice.Client, $"/api/friends/{bobPublicId}/rounds/{roundId}");
        var stats = await GetAsync<UserStatsResponse>(alice.Client, $"/api/friends/{bobPublicId}/stats");

        Assert.Equal(roundId, Assert.Single(rounds).RoundId);
        Assert.Equal(18, round.Holes.Count);
        Assert.Equal(1, stats.TotalRounds);
    }

    [Fact]
    public async Task Friends_cannot_see_a_round_still_in_progress()
    {
        var alice = await SignInNewUserAsync();
        var bob = await SignInNewUserAsync();
        await MakeFriendsAsync(alice, bob);
        var course = await Data.CreateCourseAsync();
        var open = await StartRoundAsync(bob.Client, StartRequest(course));
        var bobPublicId = (await GetAsync<UserProfileResponse>(bob.Client, "/api/profile")).PublicIdentifier;

        await AssertStatusAsync(HttpStatusCode.NotFound,
            await alice.Client.GetAsync($"/api/friends/{bobPublicId}/rounds/{open.RoundId}"));
    }

    [Fact]
    public async Task Strangers_get_not_found_for_rounds_and_stats()
    {
        var stranger = await SignInNewUserAsync();
        var bob = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await PlayCompletedRoundAsync(bob.Client, course);
        var bobPublicId = (await GetAsync<UserProfileResponse>(bob.Client, "/api/profile")).PublicIdentifier;

        await AssertStatusAsync(HttpStatusCode.NotFound, await stranger.Client.GetAsync($"/api/friends/{bobPublicId}/rounds"));
        await AssertStatusAsync(HttpStatusCode.NotFound, await stranger.Client.GetAsync($"/api/friends/{bobPublicId}/rounds/{roundId}"));
        await AssertStatusAsync(HttpStatusCode.NotFound, await stranger.Client.GetAsync($"/api/friends/{bobPublicId}/stats"));
    }

    [Fact]
    public async Task A_friend_cannot_read_someone_elses_round_through_their_profile()
    {
        var alice = await SignInNewUserAsync();
        var bob = await SignInNewUserAsync();
        var carol = await SignInNewUserAsync();
        await MakeFriendsAsync(alice, bob);
        var course = await Data.CreateCourseAsync();
        var carolsRound = await PlayCompletedRoundAsync(carol.Client, course);
        var bobPublicId = (await GetAsync<UserProfileResponse>(bob.Client, "/api/profile")).PublicIdentifier;

        await AssertStatusAsync(HttpStatusCode.NotFound,
            await alice.Client.GetAsync($"/api/friends/{bobPublicId}/rounds/{carolsRound}"));
    }
}
