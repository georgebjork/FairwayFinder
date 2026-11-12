using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services;

namespace FairwayFinder.Features.Tests.Helpers;

public class PublicScorecardKeyTests
{
    private static RoundResponse Round(long id, DateOnly date) => new() { RoundId = id, DatePlayed = date };

    [Fact]
    public void A_round_is_keyed_by_its_date_and_later_rounds_that_day_get_a_suffix()
    {
        var day = new DateOnly(2026, 9, 21);

        var keys = PublicProfileService.ScorecardKeys(
        [
            Round(30, day),
            Round(10, day),
            Round(20, day.AddDays(1)),
            Round(40, day)
        ]);

        Assert.Equal("2026-09-21", keys[10]);
        Assert.Equal("2026-09-21-2", keys[30]);
        Assert.Equal("2026-09-21-3", keys[40]);
        Assert.Equal("2026-09-22", keys[20]);
    }
}
