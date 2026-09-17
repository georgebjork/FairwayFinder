using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Tests.Helpers;
using static FairwayFinder.Features.Tests.Helpers.GameScoringTestData;

namespace FairwayFinder.Features.Tests.Games;

public class GameScoringContextTests
{
    #region SettledHoleNumbers

    [Fact]
    public void SettledHoleNumbers_covers_the_whole_set_when_every_hole_is_in()
    {
        var context = Context(GameType.Skins, [
            Line(1, AtPar()),
            Line(2, AtPar())
        ]);

        Assert.Equal(18, context.SettledHoleNumbers.Count);
    }

    [Fact]
    public void SettledHoleNumbers_stops_at_the_first_hole_someone_has_not_scored()
    {
        // Player 2 skipped hole 4 but kept going. Holes 5+ are played but not settled — the
        // result on them can still change when hole 4 comes in.
        var withGap = AtPar();
        withGap[4] = null;

        var context = Context(GameType.Skins, [
            Line(1, AtPar()),
            Line(2, withGap)
        ]);

        Assert.Equal([1, 2, 3], context.SettledHoleNumbers);
    }

    [Fact]
    public void SettledHoleNumbers_stops_at_a_hole_nobody_has_reached()
    {
        var context = Context(GameType.Skins, [
            Line(1, AtPar(through: 9)),
            Line(2, AtPar(through: 9))
        ]);

        Assert.Equal(9, context.SettledHoleNumbers.Count);
        Assert.Equal(9, context.SettledHoleNumbers[^1]);
    }

    [Fact]
    public void SettledHoleNumbers_is_empty_when_a_participant_has_scored_nothing()
    {
        var context = Context(GameType.Skins, [
            Line(1, AtPar()),
            Line(2, new Dictionary<int, int?>())
        ]);

        Assert.Empty(context.SettledHoleNumbers);
    }

    [Fact]
    public void SettledHoleNumbers_follows_the_games_own_hole_set_on_a_back_nine()
    {
        var back = Enumerable.Range(10, 9).ToDictionary(n => n, n => (int?)Pars[n - 1]);

        var context = Context(GameType.Skins, [Line(1, back), Line(2, back)],
            holeNumbers: [.. Enumerable.Range(10, 9)]);

        Assert.Equal(9, context.SettledHoleNumbers.Count);
        Assert.Equal(10, context.SettledHoleNumbers[0]);
    }

    #endregion

    #region GameSides.Resolve

    [Fact]
    public void Resolve_groups_an_unteamed_field_into_one_side_each()
    {
        var sides = GameSides.Resolve([Line(1, AtPar()), Line(2, AtPar())]);

        Assert.NotNull(sides);
        Assert.Equal(2, sides.Count);
        Assert.All(sides, s => Assert.Single(s.Members));
        Assert.All(sides, s => Assert.Null(s.Team));
    }

    [Fact]
    public void Resolve_groups_a_teamed_field_by_team()
    {
        var sides = GameSides.Resolve([
            Line(1, AtPar(), team: 1),
            Line(2, AtPar(), team: 2),
            Line(3, AtPar(), team: 1),
            Line(4, AtPar(), team: 2)
        ]);

        Assert.NotNull(sides);
        Assert.Equal(2, sides.Count);
        Assert.All(sides, s => Assert.Equal(2, s.Members.Count));
        Assert.Equal(1, sides[0].Team);
        Assert.Equal([1L, 3L], sides[0].Members.Select(m => m.ParticipantId));
    }

    [Fact]
    public void Resolve_returns_null_for_a_field_that_mixes_teamed_and_unteamed_players()
    {
        var sides = GameSides.Resolve([
            Line(1, AtPar(), team: 1),
            Line(2, AtPar())
        ]);

        Assert.Null(sides);
    }

    [Fact]
    public void Resolve_handles_an_empty_field()
    {
        Assert.Empty(GameSides.Resolve([])!);
    }

    [Fact]
    public void A_team_side_names_both_members()
    {
        var sides = GameSides.Resolve([
            Line(1, AtPar(), displayName: "Dale", team: 1),
            Line(2, AtPar(), displayName: "Sam", team: 1)
        ]);

        Assert.Equal("Dale / Sam", sides![0].DisplayName);
    }

    #endregion

    #region GameSides.BestBall

    [Fact]
    public void BestBall_takes_the_lower_gross_score_on_the_hole()
    {
        var side = GameSides.Resolve([
            Line(1, new Dictionary<int, int?> { [1] = 5 }, team: 1),
            Line(2, new Dictionary<int, int?> { [1] = 3 }, team: 1)
        ])![0];

        Assert.Equal(3, GameSides.BestBall(side, holeNumber: 1, useNet: false));
    }

    [Fact]
    public void BestBall_takes_the_lower_net_score_when_the_game_plays_net()
    {
        // Gross 5 with a shot beats gross 4 without one.
        var side = GameSides.Resolve([
            Line(1, new Dictionary<int, int?> { [1] = 5 },
                 team: 1, strokesReceived: new Dictionary<int, int> { [1] = 2 }),
            Line(2, new Dictionary<int, int?> { [1] = 4 }, team: 1)
        ])![0];

        Assert.Equal(3, GameSides.BestBall(side, holeNumber: 1, useNet: true));
        Assert.Equal(4, GameSides.BestBall(side, holeNumber: 1, useNet: false));
    }

    [Fact]
    public void BestBall_ignores_a_member_who_has_not_played_the_hole()
    {
        var side = GameSides.Resolve([
            Line(1, new Dictionary<int, int?> { [1] = null }, team: 1),
            Line(2, new Dictionary<int, int?> { [1] = 4 }, team: 1)
        ])![0];

        Assert.Equal(4, GameSides.BestBall(side, holeNumber: 1, useNet: false));
    }

    [Fact]
    public void BestBall_is_null_when_nobody_on_the_side_has_played_the_hole()
    {
        var side = GameSides.Resolve([
            Line(1, new Dictionary<int, int?> { [1] = null }, team: 1)
        ])![0];

        Assert.Null(GameSides.BestBall(side, holeNumber: 1, useNet: false));
    }

    #endregion
}
