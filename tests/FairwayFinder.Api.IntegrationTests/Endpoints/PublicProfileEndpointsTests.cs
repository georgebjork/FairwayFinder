using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FairwayFinder.Api.Endpoints;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using static FairwayFinder.Api.IntegrationTests.Infrastructure.RoundFlows;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

/// <summary>
/// The anonymous public stats page (<c>GET /api/public/profiles/{slug}</c>) and the settings that
/// control it (<c>PUT /api/profile/public</c>).
/// </summary>
public class PublicProfileEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static Task<HttpResponseMessage> PutSettingsAsync(HttpClient client, UpdatePublicProfileRequest request) =>
        client.PutAsJsonAsync("/api/profile/public", request, Json);

    // ── The public page ─────────────────────────────────────

    [Fact]
    public async Task A_public_profile_is_served_anonymously_with_its_stats()
    {
        var golfer = await SignInNewUserAsync(firstName: "Pat");
        var course = await Data.CreateCourseAsync();
        for (var i = 0; i < 3; i++)
            await PlayCompletedRoundAsync(golfer.Client, course, overPar: 1, datePlayed: Today.AddDays(-i));
        await AssertStatusAsync(HttpStatusCode.OK, await PutSettingsAsync(golfer.Client, new UpdatePublicProfileRequest(true, "pat-golfs")));

        using var anonymous = AnonymousClient();
        var response = await anonymous.GetAsync("/api/public/profiles/pat-golfs");
        var profile = await ReadAsync<PublicProfileResponse>(response);

        Assert.Equal("pat-golfs", profile.Slug);
        Assert.StartsWith("Pat ", profile.DisplayName);
        Assert.Equal(3, profile.Headline.TotalRounds);
        Assert.Equal(3, profile.Headline.RoundsThisYear);
        Assert.Equal(90, profile.Headline.ScoringAverage18);
        Assert.Equal(90, profile.Headline.BestRound!.Score);
        Assert.Equal(18, profile.Headline.BestRound.ToPar);
        Assert.Equal(course.CourseName, profile.Headline.BestRound.CourseName);
        // Rating 71.5 / slope 130: differential 16.1 each; three rounds → lowest − 2.0.
        Assert.Equal(14.1m, profile.Headline.HandicapIndex);
        Assert.Equal(54, profile.ScoringDistribution.Bogeys);
        Assert.Equal(54, profile.ScoringDistribution.TotalHoles);
        Assert.Equal(3, profile.Trend.Count);
        Assert.Equal(3, profile.RecentRounds!.Count);
        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task The_response_carries_nothing_that_identifies_the_account()
    {
        var golfer = await SignInNewUserAsync();
        await Data.MakeProfilePublicAsync(golfer.Id, "no-leaks");

        using var anonymous = AnonymousClient();
        var json = await (await anonymous.GetAsync("/api/public/profiles/no-leaks")).Content.ReadAsStringAsync();

        Assert.DoesNotContain(golfer.Email, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(golfer.Id, json);
        Assert.DoesNotContain(golfer.User.Handle, json);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publicIdentifier", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Private_and_unknown_slugs_are_indistinguishable_404s()
    {
        var golfer = await SignInNewUserAsync();
        await Data.MakeProfilePublicAsync(golfer.Id, "gone-quiet", isPublic: false);

        using var anonymous = AnonymousClient();
        var privateResponse = await anonymous.GetAsync("/api/public/profiles/gone-quiet");
        var unknownResponse = await anonymous.GetAsync("/api/public/profiles/never-existed");

        await AssertStatusAsync(HttpStatusCode.NotFound, privateResponse);
        await AssertStatusAsync(HttpStatusCode.NotFound, unknownResponse);

        // Same body apart from the slug echoed back and the per-request trace id, so the body can't
        // tell them apart either.
        Assert.Equal(
            await NormalizedBodyAsync(privateResponse, "gone-quiet"),
            await NormalizedBodyAsync(unknownResponse, "never-existed"));
    }

    private static async Task<string> NormalizedBodyAsync(HttpResponseMessage response, string slug)
    {
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        body.Remove("traceId");
        return body.ToJsonString().Replace(slug, "{slug}");
    }

    [Fact]
    public async Task Turning_the_profile_off_takes_the_page_down()
    {
        var golfer = await SignInNewUserAsync();
        await PutSettingsAsync(golfer.Client, new UpdatePublicProfileRequest(true, "on-and-off"));
        using var anonymous = AnonymousClient();
        await AssertStatusAsync(HttpStatusCode.OK, await anonymous.GetAsync("/api/public/profiles/on-and-off"));

        await PutSettingsAsync(golfer.Client, new UpdatePublicProfileRequest(false, null));

        await AssertStatusAsync(HttpStatusCode.NotFound, await anonymous.GetAsync("/api/public/profiles/on-and-off"));
    }

    [Fact]
    public async Task Slug_lookup_ignores_case()
    {
        var golfer = await SignInNewUserAsync();
        await Data.MakeProfilePublicAsync(golfer.Id, "mixed-case");

        using var anonymous = AnonymousClient();

        await AssertStatusAsync(HttpStatusCode.OK, await anonymous.GetAsync("/api/public/profiles/Mixed-CASE"));
    }

    [Fact]
    public async Task A_round_still_being_entered_does_not_count()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, course, datePlayed: Today);
        var open = await StartRoundAsync(golfer.Client, StartRequest(course, Today));
        await UpsertHoleAsync(golfer.Client, open.RoundId, 1, 1);
        await Data.MakeProfilePublicAsync(golfer.Id, "in-progress");

        using var anonymous = AnonymousClient();
        var profile = await GetAsync<PublicProfileResponse>(anonymous, "/api/public/profiles/in-progress");

        Assert.Equal(1, profile.Headline.TotalRounds);
        Assert.Single(profile.RecentRounds!);
        Assert.Equal(72, profile.Headline.BestRound!.Score);
    }

    [Fact]
    public async Task Hidden_sections_are_left_out()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, course, datePlayed: Today);
        await Data.MakeProfilePublicAsync(golfer.Id, "low-profile",
            showStrokesGained: false, showRecentRounds: false, showCourseNames: false);

        using var anonymous = AnonymousClient();
        var json = await (await anonymous.GetAsync("/api/public/profiles/low-profile")).Content.ReadAsStringAsync();
        var profile = await GetAsync<PublicProfileResponse>(anonymous, "/api/public/profiles/low-profile");

        Assert.Null(profile.StrokesGained);
        Assert.Null(profile.RecentRounds);
        Assert.Null(profile.Headline.BestRound!.CourseName);
        Assert.All(profile.Trend, p => Assert.Null(p.CourseName));
        Assert.DoesNotContain(course.CourseName, json);
    }

    [Fact]
    public async Task Recent_rounds_link_to_hole_by_hole_scorecards_keyed_by_date()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        var day = Today.AddDays(-3);
        var first = await PlayCompletedRoundAsync(golfer.Client, course, overPar: 1, datePlayed: day);
        await PlayCompletedRoundAsync(golfer.Client, course, overPar: 0, datePlayed: day);
        await Data.MakeProfilePublicAsync(golfer.Id, "cards");

        using var anonymous = AnonymousClient();
        var json = await (await anonymous.GetAsync("/api/public/profiles/cards")).Content.ReadAsStringAsync();
        var profile = await GetAsync<PublicProfileResponse>(anonymous, "/api/public/profiles/cards");

        var key = day.ToString("yyyy-MM-dd");
        Assert.Equal([key, $"{key}-2"], profile.Scorecards.Select(c => c.Key).Order());
        Assert.All(profile.RecentRounds!, r => Assert.Contains(r.Key, profile.Scorecards.Select(c => c.Key)));
        Assert.Equal($"{key}-2", profile.Headline.BestRound!.Key);

        var card = profile.Scorecards.Single(c => c.Key == key);
        Assert.Equal(90, card.Score);
        Assert.Equal(18, card.ToPar);
        Assert.Equal(72, card.Par);
        Assert.Equal(course.CourseName, card.CourseName);
        Assert.Equal(Enumerable.Range(1, 18), card.Holes.Select(h => h.HoleNumber));
        Assert.All(card.Holes, h => Assert.Equal(h.Par + 1, h.Score));
        Assert.Null(card.Putts); // posted without hole stats

        // The key is a date, never the database id.
        Assert.DoesNotContain($":{first},", json);
        Assert.DoesNotContain("roundId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hiding_recent_rounds_publishes_no_scorecards()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, course, datePlayed: Today);
        await Data.MakeProfilePublicAsync(golfer.Id, "no-cards", showRecentRounds: false);

        using var anonymous = AnonymousClient();
        var profile = await GetAsync<PublicProfileResponse>(anonymous, "/api/public/profiles/no-cards");

        Assert.Empty(profile.Scorecards);
        Assert.Null(profile.Headline.BestRound!.Key);
    }

    [Fact]
    public async Task Hiding_course_names_hides_them_on_scorecards_too()
    {
        var golfer = await SignInNewUserAsync();
        var course = await Data.CreateCourseAsync();
        await PlayCompletedRoundAsync(golfer.Client, course, datePlayed: Today);
        await Data.MakeProfilePublicAsync(golfer.Id, "anon-cards", showCourseNames: false);

        using var anonymous = AnonymousClient();
        var json = await (await anonymous.GetAsync("/api/public/profiles/anon-cards")).Content.ReadAsStringAsync();
        var profile = await GetAsync<PublicProfileResponse>(anonymous, "/api/public/profiles/anon-cards");

        Assert.Null(Assert.Single(profile.Scorecards).CourseName);
        Assert.DoesNotContain(course.CourseName, json);
    }

    // ── Settings ────────────────────────────────────────────

    [Fact]
    public async Task Going_public_without_a_slug_takes_one_from_the_username()
    {
        var golfer = await SignInNewUserAsync();
        await golfer.Client.PutAsJsonAsync("/api/profile/username", new UpdateUsernameRequest("tee.time_42"), Json);

        var profile = await ReadAsync<UserProfileResponse>(
            await PutSettingsAsync(golfer.Client, new UpdatePublicProfileRequest(true, null)));

        Assert.True(profile.IsPublic);
        Assert.Equal("tee-time-42", profile.PublicSlug);
    }

    [Fact]
    public async Task Settings_round_trip_through_the_profile()
    {
        var golfer = await SignInNewUserAsync();

        await AssertStatusAsync(HttpStatusCode.OK, await PutSettingsAsync(golfer.Client,
            new UpdatePublicProfileRequest(true, "  Round-Trip ", ShowStrokesGained: false, ShowRecentRounds: true, ShowCourseNames: false)));

        var profile = await GetAsync<UserProfileResponse>(golfer.Client, "/api/profile");
        Assert.True(profile.IsPublic);
        Assert.Equal("round-trip", profile.PublicSlug);
        Assert.False(profile.ShowStrokesGained);
        Assert.True(profile.ShowRecentRounds);
        Assert.False(profile.ShowCourseNames);
    }

    [Fact]
    public async Task A_new_profile_is_private_with_every_section_shown()
    {
        var golfer = await SignInNewUserAsync();

        var profile = await GetAsync<UserProfileResponse>(golfer.Client, "/api/profile");

        Assert.False(profile.IsPublic);
        Assert.Null(profile.PublicSlug);
        Assert.True(profile.ShowStrokesGained);
        Assert.True(profile.ShowRecentRounds);
        Assert.True(profile.ShowCourseNames);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("has spaces")]
    [InlineData("under_score")]
    [InlineData("admin")]
    public async Task Invalid_or_reserved_slugs_are_rejected(string slug)
    {
        var golfer = await SignInNewUserAsync();

        await AssertStatusAsync(HttpStatusCode.BadRequest,
            await PutSettingsAsync(golfer.Client, new UpdatePublicProfileRequest(true, slug)));
    }

    [Fact]
    public async Task A_slug_someone_else_holds_conflicts()
    {
        var first = await SignInNewUserAsync();
        await Data.MakeProfilePublicAsync(first.Id, "first-dibs");
        var second = await SignInNewUserAsync();

        await AssertStatusAsync(HttpStatusCode.Conflict,
            await PutSettingsAsync(second.Client, new UpdatePublicProfileRequest(true, "First-Dibs")));

        // Your own slug is not a conflict with yourself.
        await AssertStatusAsync(HttpStatusCode.OK,
            await PutSettingsAsync(first.Client, new UpdatePublicProfileRequest(true, "first-dibs")));
    }

    [Fact]
    public async Task Settings_require_sign_in()
    {
        using var anonymous = AnonymousClient();

        await AssertStatusAsync(HttpStatusCode.Unauthorized,
            await PutSettingsAsync(anonymous, new UpdatePublicProfileRequest(true, "sneaky")));
    }
}
