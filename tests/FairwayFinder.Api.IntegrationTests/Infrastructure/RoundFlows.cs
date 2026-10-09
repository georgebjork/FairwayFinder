using System.Net;
using System.Net.Http.Json;
using FairwayFinder.Features.Data;
using FairwayFinder.IntegrationTests.Common;

namespace FairwayFinder.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The hole-by-hole flow the iOS app drives, as reusable steps for any test that needs a round.
/// </summary>
public static class RoundFlows
{
    public static readonly DateOnly DefaultDate = new(2026, 7, 1);

    public static StartRoundRequest StartRequest(SeededCourse course, DateOnly? datePlayed = null,
        bool fullRound = true, bool frontNine = false, bool backNine = false) => new()
    {
        CourseId = course.CourseId,
        TeeboxId = course.TeeboxId,
        DatePlayed = datePlayed ?? DefaultDate,
        FullRound = fullRound,
        FrontNine = frontNine,
        BackNine = backNine,
    };

    public static async Task<StartRoundResponse> StartRoundAsync(HttpClient client, StartRoundRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/rounds/start", request);
        if (response.StatusCode != HttpStatusCode.Created)
            Assert.Fail($"Starting a round returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<StartRoundResponse>())!;
    }

    public static async Task<RoundProgressResponse> UpsertHoleAsync(HttpClient client, long roundId, int holeNumber, short score)
    {
        var response = await client.PutAsJsonAsync($"/api/rounds/{roundId}/holes/{holeNumber}",
            new UpsertHoleRequest { Score = score });
        if (response.StatusCode != HttpStatusCode.OK)
            Assert.Fail($"Writing hole {holeNumber} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<RoundProgressResponse>())!;
    }

    /// <summary>
    /// Starts, plays and posts a full round. <paramref name="overPar"/> strokes are added to each
    /// hole's par, so the total is <c>course.Par + overPar * holes</c>.
    /// </summary>
    public static async Task<long> PlayCompletedRoundAsync(HttpClient client, SeededCourse course,
        int overPar = 0, DateOnly? datePlayed = null)
    {
        var started = await StartRoundAsync(client, StartRequest(course, datePlayed));

        foreach (var hole in started.Holes)
            await UpsertHoleAsync(client, started.RoundId, hole.HoleNumber, (short)(hole.Par + overPar));

        var complete = await client.PostAsync($"/api/rounds/{started.RoundId}/complete", null);
        if (complete.StatusCode != HttpStatusCode.OK)
            Assert.Fail($"Posting the round returned {(int)complete.StatusCode}: {await complete.Content.ReadAsStringAsync()}");

        return started.RoundId;
    }
}
