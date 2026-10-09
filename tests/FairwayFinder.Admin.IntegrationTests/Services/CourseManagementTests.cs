using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Admin.IntegrationTests.Services;

/// <summary>Course and teebox data entry from the console, against the real schema.</summary>
public class CourseManagementTests(AdminFactory factory) : AdminTestBase(factory)
{
    private static SaveTeeboxRequest Teebox(long courseId, string name = "Blue", long? teeboxId = null, decimal rating = 71.2m) => new()
    {
        TeeboxId = teeboxId,
        CourseId = courseId,
        TeeboxName = name,
        Rating = rating,
        Slope = 128,
        Holes = TestData.StandardPars.Select((par, i) => new HoleEntry
        {
            HoleNumber = i + 1, Par = par, Yardage = 380, Handicap = i + 1,
        }).ToList(),
    };

    [Fact]
    public async Task Create_course_and_teebox_then_read_them_back()
    {
        var admin = await Data.CreateAdminAsync();
        var courses = Service<ICourseService>();

        var courseId = await courses.CreateCourseAsync(new SaveCourseRequest { CourseName = "Console Course", Address = "1 Fairway Dr" }, admin.Id);
        var teeboxId = await courses.CreateTeeboxAsync(Teebox(courseId), admin.Id);

        var detail = (await courses.GetCourseDetailAsync(courseId))!;
        Assert.Equal("Console Course", detail.CourseName);
        var teebox = Assert.Single(detail.Teeboxes);
        Assert.Equal(teeboxId, teebox.TeeboxId);
        Assert.Equal(72, teebox.Par);
        Assert.Equal(18 * 380, teebox.YardageTotal);

        Assert.Single(await courses.GetAllCoursesAsync(), c => c.CourseId == courseId && c.TeeboxCount == 1);

        await using var db = Db();
        Assert.Equal(admin.Id, (await db.Courses.SingleAsync(c => c.CourseId == courseId)).CreatedBy);
    }

    [Fact]
    public async Task Editing_a_teebox_updates_its_holes_in_place()
    {
        var admin = await Data.CreateAdminAsync();
        var courses = Service<ICourseService>();
        var courseId = await courses.CreateCourseAsync(new SaveCourseRequest { CourseName = "Edit Course" }, admin.Id);
        var teeboxId = await courses.CreateTeeboxAsync(Teebox(courseId), admin.Id);
        var holeIds = (await courses.GetHolesAsync(teeboxId)).Select(h => h.HoleId).Order().ToList();

        var edit = Teebox(courseId, name: "Blue Tips", teeboxId: teeboxId);
        edit.Holes = (await courses.GetHolesAsync(teeboxId)).Select(h => new HoleEntry
        {
            HoleNumber = h.HoleNumber, Par = h.Par, Yardage = 420, Handicap = h.Handicap,
        }).ToList();
        Assert.True(await courses.UpdateTeeboxAsync(edit, admin.Id));

        var holes = await courses.GetHolesAsync(teeboxId);
        Assert.All(holes, h => Assert.Equal(420, h.Yardage));
        Assert.Equal(holeIds, holes.Select(h => h.HoleId).Order());
    }

    [Fact]
    public async Task Re_rating_a_played_teebox_creates_a_new_version_and_leaves_old_rounds_alone()
    {
        var admin = await Data.CreateAdminAsync();
        var golfer = await Data.CreateUserAsync();
        var course = await Data.CreateCourseAsync();
        var roundId = await Data.CreateCompletedRoundAsync(golfer.Id, course);
        var courses = Service<ICourseService>();

        var newVersionId = await courses.CreateTeeboxVersionAsync(
            Teebox(course.CourseId, teeboxId: course.TeeboxId, rating: 73.4m), admin.Id);

        Assert.NotEqual(course.TeeboxId, newVersionId);

        await using var db = Db();
        Assert.Equal(course.TeeboxId, (await db.Rounds.SingleAsync(r => r.RoundId == roundId)).TeeboxId);
        Assert.Equal(73.4m, (await db.Teeboxes.SingleAsync(t => t.TeeboxId == newVersionId)).Rating);

        // Only the current version is offered for new rounds.
        var offered = await courses.GetTeeboxesAsync(course.CourseId);
        Assert.Equal(newVersionId, Assert.Single(offered).TeeboxId);
    }

    [Fact]
    public async Task Deleting_a_course_hides_it_from_search()
    {
        var admin = await Data.CreateAdminAsync();
        var course = await Data.CreateCourseAsync("Doomed Test Course");
        var courses = Service<ICourseService>();

        Assert.True(await courses.DeleteCourseAsync(course.CourseId, admin.Id));

        Assert.Empty(await courses.SearchCoursesAsync("Doomed"));
        Assert.Null(await courses.GetCourseDetailAsync(course.CourseId));
    }
}
