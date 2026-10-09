using System.Net;
using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data.GolfCourseApi;
using FairwayFinder.Features.Data.TGTR;
using FairwayFinder.Features.Services.GolfCourseApi;
using FairwayFinder.Features.Services.TGTR;
using FairwayFinder.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Admin.IntegrationTests.Services;

/// <summary>
/// The two data-import tools, driven end to end through their typed HttpClients against canned
/// third-party responses, asserting on the rows they write.
/// </summary>
public class ExternalImportTests(AdminFactory factory) : AdminTestBase(factory)
{
    // ── GolfCourseAPI ──

    private static GolfCourseApiTeeBox Tee(string name, int holes = 18) => new()
    {
        TeeName = name,
        CourseRating = 72.1m,
        SlopeRating = 131,
        NumberOfHoles = holes,
        ParTotal = TestData.StandardPars.Take(holes).Sum(),
        TotalYards = 400 * holes,
        Holes = TestData.StandardPars.Take(holes)
            .Select((par, i) => new GolfCourseApiHole { Par = par, Yardage = 400, Handicap = i + 1 })
            .ToList(),
    };

    private void StubCoursesPage(params GolfCourseApiCourse[] courses) =>
        Factory.ExternalHttp.RespondJson("/v1/courses", new GolfCourseApiCoursesResponse
        {
            Courses = courses.ToList(),
            Metadata = new GolfCourseApiMetadata { CurrentPage = 1, FirstPage = 1, LastPage = 1, TotalRecords = courses.Length },
        });

    [Fact]
    public async Task Golf_course_api_import_creates_courses_teeboxes_and_holes_with_a_mapping()
    {
        StubCoursesPage(new GolfCourseApiCourse
        {
            Id = 101,
            ClubName = "Imported Club",
            CourseName = "Imported Championship",
            Location = new GolfCourseApiLocation { City = "Monterey", State = "CA" },
            Tees = new GolfCourseApiTees { Male = [Tee("Blue"), Tee("White")], Female = [Tee("Red")] },
        });

        var result = await Service<GolfCourseApiImportService>().ImportAllCoursesAsync();

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.CoursesImported);

        await using var db = Db();
        var map = await db.GolfCourseApiCourseMaps.SingleAsync(m => m.ApiCourseId == 101);
        var course = await db.Courses.SingleAsync(c => c.CourseId == map.CourseId);
        Assert.Equal("Monterey", course.City);

        var teeboxes = await db.Teeboxes.Where(t => t.CourseId == course.CourseId).ToListAsync();
        Assert.Equal(3, teeboxes.Count);
        Assert.Single(teeboxes, t => t.IsWomens);
        Assert.Equal(54, await db.Holes.CountAsync(h => h.CourseId == course.CourseId));

        // The API key travels on every request.
        Assert.All(Factory.ExternalHttp.Requests, r => Assert.Equal("Key test-key", r.Headers.Authorization?.ToString()));
    }

    [Fact]
    public async Task Golf_course_api_reimport_updates_in_place_rather_than_duplicating()
    {
        var course = new GolfCourseApiCourse
        {
            Id = 202,
            ClubName = "Repeat Club",
            CourseName = "Repeat Course",
            Tees = new GolfCourseApiTees { Male = [Tee("Blue")] },
        };
        StubCoursesPage(course);
        var service = Service<GolfCourseApiImportService>();
        await service.ImportAllCoursesAsync();

        course.Location = new GolfCourseApiLocation { City = "Pinehurst" };
        StubCoursesPage(course);
        var second = await service.ImportAllCoursesAsync();

        Assert.Equal(0, second.CoursesImported);

        await using var db = Db();
        Assert.Equal(1, await db.Courses.CountAsync(c => !c.IsDeleted));
        Assert.Equal(1, await db.Teeboxes.CountAsync(t => !t.IsDeleted));
        Assert.Equal(18, await db.Holes.CountAsync(h => !h.IsDeleted));
    }

    [Fact]
    public async Task Golf_course_api_courses_without_tees_are_skipped()
    {
        StubCoursesPage(new GolfCourseApiCourse { Id = 303, CourseName = "No Tees", ClubName = "Empty" });

        var result = await Service<GolfCourseApiImportService>().ImportAllCoursesAsync();

        Assert.Equal(1, result.CoursesSkipped);
        await using var db = Db();
        Assert.False(await db.Courses.AnyAsync());
    }

    [Fact]
    public async Task Golf_course_api_outage_fails_the_import_without_writing_anything()
    {
        Factory.ExternalHttp.Respond("/v1/courses", _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<HttpRequestException>(() => Service<GolfCourseApiImportService>().ImportAllCoursesAsync());

        await using var db = Db();
        Assert.False(await db.Courses.AnyAsync());
    }

    // ── TGTR ──

    private static TgtrRoundResponse TgtrRound(int id, SeededCourse course, int overPar = 0) => new()
    {
        Id = id,
        PlayerId = 77,
        CourseId = 500,
        CourseName = course.CourseName,
        TeeBoxId = 600,
        TeeBoxName = "Blue",
        Date = new DateTime(2023, 6, 1),
        Year = 2023,
        Par = course.Par,
        Score = course.Par + overPar * 18,
        FinishedRound = true,
        FinishedFront = true,
        FinishedBack = true,
        HolesPlayed = 18,
        HoleScores = course.Pars.Select((par, i) => new TgtrHoleScore
        {
            Number = i + 1,
            Par = par,
            Score = par + overPar,
            ScoreToPar = overPar,
        }).ToList(),
    };

    [Fact]
    public async Task Tgtr_transfer_imports_rounds_for_the_mapped_player_matching_course_and_teebox_by_name()
    {
        var admin = await Data.CreateAdminAsync();
        var golfer = await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync("Legacy Links");
        var service = Service<TgtrTransferService>();
        await service.AddPlayerMapAsync(77, golfer.Id, admin.Id);
        Factory.ExternalHttp.RespondJson("/api/players/77/rounds", new List<TgtrRoundResponse>
        {
            TgtrRound(1, course),
            TgtrRound(2, course, overPar: 1),
        });

        var result = await service.TransferRoundsAsync(77);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.RoundsImported);

        await using var db = Db();
        var rounds = await db.Rounds.Where(r => r.UserId == golfer.Id).ToListAsync();
        Assert.Equal(2, rounds.Count);
        Assert.All(rounds, r => Assert.True(r.IsComplete));
        Assert.Contains(rounds, r => r.Score == course.Par + 18);
        Assert.Single(await db.TgtrCourseMaps.ToListAsync());

        // Running it again skips everything already brought across.
        var again = await service.TransferRoundsAsync(77);
        Assert.Equal(0, again.RoundsImported);
        Assert.Equal(2, again.RoundsSkipped);
    }

    [Fact]
    public async Task Tgtr_transfer_without_a_player_mapping_does_nothing()
    {
        var result = await Service<TgtrTransferService>().TransferRoundsAsync(999);

        Assert.Equal(0, result.RoundsImported);
        Assert.Contains("No player mapping", Assert.Single(result.Errors).Reason);
        Assert.Empty(Factory.ExternalHttp.Requests);
    }

    [Fact]
    public async Task Tgtr_rounds_on_unknown_courses_are_reported_per_round()
    {
        var admin = await Data.CreateAdminAsync();
        var golfer = await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync("Known Course");
        var service = Service<TgtrTransferService>();
        await service.AddPlayerMapAsync(77, golfer.Id, admin.Id);

        var unknown = TgtrRound(3, course);
        unknown.CourseId = 501;
        unknown.CourseName = "Nowhere Golf Club";
        Factory.ExternalHttp.RespondJson("/api/players/77/rounds", new List<TgtrRoundResponse> { TgtrRound(4, course), unknown });

        var result = await service.TransferRoundsAsync(77);

        Assert.Equal(1, result.RoundsImported);
        Assert.Equal(3, Assert.Single(result.Errors).TgtrRoundId);
    }
}
