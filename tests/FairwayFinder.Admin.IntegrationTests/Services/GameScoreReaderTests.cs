using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.IntegrationTests.Common;

namespace FairwayFinder.Admin.IntegrationTests.Services;

/// <summary>
/// The stroke-index fallback in <see cref="GameScoreReader"/>: a teebox with unset indexes borrows
/// from its own lineage, and only its own lineage.
/// </summary>
public class GameScoreReaderTests(AdminFactory factory) : AdminTestBase(factory)
{
    private static Game FullRoundGame(long courseId) => new()
    {
        GameType = GameType.MatchPlay,
        CourseId = courseId,
        FullRound = true,
        HandicapAllowancePercent = 100,
    };

    private static GameParticipant Golfer(long teeboxId) => new()
    {
        GameParticipantId = 1,
        DisplayName = "Golfer",
        TeeboxId = teeboxId,
    };

    private async Task<IReadOnlyDictionary<int, GameHoleLine>> HolesReadForAsync(SeededCourse course)
    {
        var read = await Service<GameScoreReader>().ReadAsync(FullRoundGame(course.CourseId), [Golfer(course.TeeboxId)]);
        return Assert.Single(read.Context.Participants).Holes;
    }

    [Fact]
    public async Task Unset_stroke_index_is_borrowed_from_an_earlier_version_of_the_same_tee()
    {
        var admin = await Data.CreateAdminAsync();
        var original = await Data.CreateCourseAsync();

        // A new version whose stroke indexes were left blank.
        var versionId = await Service<ICourseService>().CreateTeeboxVersionAsync(new SaveTeeboxRequest
        {
            TeeboxId = original.TeeboxId,
            CourseId = original.CourseId,
            TeeboxName = "Blue",
            Rating = 71.5m,
            Slope = 130,
            Holes = original.Pars.Select((par, i) => new HoleEntry { HoleNumber = i + 1, Par = par, Yardage = 400, Handicap = 0 }).ToList(),
        }, admin.Id);

        var holes = await HolesReadForAsync(original with { TeeboxId = versionId });

        Assert.All(holes.Values, h => Assert.Equal(h.HoleNumber, h.StrokeIndex));
    }

    [Theory]
    [InlineData(0L)]         // legacy rows the GolfCourseAPI importer used to leave without a lineage
    [InlineData(987_654L)]   // a group id that collides across courses: still not one tee
    public async Task Unset_stroke_index_is_never_borrowed_from_another_course(long sharedGroupId)
    {
        await Data.CreateCourseAsync(teeboxGroupId: sharedGroupId);
        var unindexed = await Data.CreateCourseAsync(teeboxGroupId: sharedGroupId, withStrokeIndexes: false);

        var holes = await HolesReadForAsync(unindexed);

        Assert.All(holes.Values, h => Assert.Equal(0, h.StrokeIndex));
    }
}
