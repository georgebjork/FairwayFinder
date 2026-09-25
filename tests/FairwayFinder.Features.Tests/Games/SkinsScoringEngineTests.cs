using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using static FairwayFinder.Features.Tests.Helpers.GameScoringTestData;

namespace FairwayFinder.Features.Tests.Games;

public class SkinsScoringEngineTests
{
    private static readonly SkinsScoringEngine Engine = new();

    /// <summary>
    /// A two-player skins game over the first <paramref name="holeCount"/> holes, where each
    /// player's scores are given hole by hole.
    /// </summary>
    private static SkinsScoreboard Play(
        IReadOnlyList<int?> playerOne,
        IReadOnlyList<int?> playerTwo,
        int holeCount = 18,
        bool carryover = true,
        bool useNet = false,
        decimal? skinsValue = null,
        IReadOnlyDictionary<int, int>? oneStrokes = null,
        IReadOnlyDictionary<int, int>? twoStrokes = null)
    {
        static Dictionary<int, int?> ToHoles(IReadOnlyList<int?> scores)
            => scores.Select((s, i) => (Hole: i + 1, Score: s)).ToDictionary(x => x.Hole, x => x.Score);

        var context = Context(
            GameType.Skins,
            [
                Line(1, ToHoles(playerOne), displayName: "Dale", strokesReceived: oneStrokes),
                Line(2, ToHoles(playerTwo), displayName: "Sam", strokesReceived: twoStrokes)
            ],
            holeCount,
            Rules(useNet: useNet, skinsCarryover: carryover, skinsValue: skinsValue));

        return Engine.Score(context);
    }

    [Fact]
    public void Score_awards_a_skin_to_the_sole_low_score()
    {
        var board = Play([4, 4, 4], [5, 5, 5], holeCount: 3);

        Assert.Equal(3, board.Tallies.Single(t => t.ParticipantId == 1).SkinsWon);
        Assert.Equal(0, board.Tallies.Single(t => t.ParticipantId == 2).SkinsWon);
        Assert.All(board.Holes, h => Assert.Equal(1L, h.WonByParticipantId));
    }

    [Fact]
    public void Score_carries_a_tied_hole_into_the_next_one()
    {
        // Hole 1 tied, hole 2 won by player 1 — worth two skins.
        var board = Play([4, 3], [4, 5], holeCount: 2);

        Assert.True(board.Holes[0].IsTied);
        Assert.Equal(0, board.Holes[0].SkinsAwarded);
        Assert.Equal(1, board.Holes[1].CarriedIn);
        Assert.Equal(2, board.Holes[1].SkinsAwarded);
        Assert.Equal(2, board.Tallies.Single(t => t.ParticipantId == 1).SkinsWon);
    }

    [Fact]
    public void Score_carries_across_several_tied_holes()
    {
        var board = Play([4, 4, 4, 3], [4, 4, 4, 5], holeCount: 4);

        Assert.Equal(3, board.Holes[3].CarriedIn);
        Assert.Equal(4, board.Holes[3].SkinsAwarded);
        Assert.Equal(4, board.Tallies.Single(t => t.ParticipantId == 1).SkinsWon);
    }

    [Fact]
    public void Score_voids_a_tied_hole_when_carryover_is_off()
    {
        var board = Play([4, 3], [4, 5], holeCount: 2, carryover: false);

        Assert.True(board.Holes[0].IsTied);
        Assert.Equal(0, board.Holes[1].CarriedIn);
        Assert.Equal(1, board.Holes[1].SkinsAwarded);
        Assert.Equal(1, board.Tallies.Single(t => t.ParticipantId == 1).SkinsWon);
    }

    [Fact]
    public void Score_stops_at_an_unplayed_hole_so_carryover_never_jumps_a_gap()
    {
        // Hole 2 is missing for player 2. Hole 3's outright win must not collect hole 1's
        // carried skin, because hole 2 has not been settled yet.
        var board = Play([4, 4, 3], [4, null, 5], holeCount: 3);

        Assert.Equal(1, board.HolesPlayed);
        Assert.Equal(2, board.HolesRemaining);
        Assert.Single(board.Holes);
        Assert.True(board.Holes[0].IsTied);
        Assert.Equal(1, board.CarriedSkins);
        Assert.All(board.Tallies, t => Assert.Equal(0, t.SkinsWon));
    }

    [Fact]
    public void Score_uses_net_scores_when_the_game_plays_net()
    {
        // Gross: 5 vs 4, so player 2 wins gross. Net: player 1 gets a shot on hole 1, so 4 vs 4
        // is a tie and nobody wins the skin.
        var strokes = new Dictionary<int, int> { [1] = 1 };

        var gross = Play([5], [4], holeCount: 1, useNet: false, oneStrokes: strokes);
        Assert.Equal(2L, gross.Holes[0].WonByParticipantId);

        var net = Play([5], [4], holeCount: 1, useNet: true, oneStrokes: strokes);
        Assert.True(net.Holes[0].IsTied);
        Assert.Null(net.Holes[0].WonByParticipantId);
    }

    [Fact]
    public void Score_multiplies_the_tally_by_the_skin_value()
    {
        var board = Play([4, 4], [5, 5], holeCount: 2, skinsValue: 5m);

        Assert.Equal(10m, board.Tallies.Single(t => t.ParticipantId == 1).Value);
        Assert.Equal(0m, board.Tallies.Single(t => t.ParticipantId == 2).Value);
    }

    [Fact]
    public void Score_leaves_the_value_null_when_the_game_has_no_stake()
    {
        var board = Play([4], [5], holeCount: 1);

        Assert.All(board.Tallies, t => Assert.Null(t.Value));
    }

    [Fact]
    public void Score_is_not_decided_while_holes_remain()
    {
        var board = Play([4, 4], [5, 5], holeCount: 18);

        Assert.False(board.IsDecided);
        Assert.Equal(2, board.HolesPlayed);
        Assert.Equal(16, board.HolesRemaining);
    }

    [Fact]
    public void Score_is_decided_once_every_hole_is_in()
    {
        var board = Play([4, 4], [5, 5], holeCount: 2);

        Assert.True(board.IsDecided);
        Assert.Equal(0, board.HolesRemaining);
    }

    [Fact]
    public void Score_reports_the_skins_still_on_the_line()
    {
        var board = Play([4, 4], [4, 4], holeCount: 18);

        Assert.Equal(2, board.CarriedSkins);
        Assert.Contains("2 skins on the line", board.Summary);
    }

    [Fact]
    public void Score_names_the_leader_in_the_summary()
    {
        var board = Play([4, 4], [5, 5], holeCount: 18);

        Assert.Contains("Dale", board.Summary);
        Assert.Contains("2 skins", board.Summary);
    }

    [Fact]
    public void Score_ranks_standings_with_the_leader_first()
    {
        var board = Play([4, 4], [5, 5], holeCount: 2);

        Assert.Equal(1L, board.Standings[0].ParticipantId);
        Assert.Equal(1, board.Standings[0].Position);
        Assert.True(board.Standings[0].IsLeader);
        Assert.Equal(2, board.Standings[1].Position);
        Assert.False(board.Standings[1].IsLeader);
    }

    [Fact]
    public void Score_leads_nobody_when_no_skin_has_been_won()
    {
        var board = Play([4, 4], [4, 4], holeCount: 2);

        Assert.All(board.Standings, s => Assert.False(s.IsLeader));
        Assert.All(board.Standings, s => Assert.Equal(1, s.Position));
    }

    [Fact]
    public void Score_handles_a_game_with_no_holes_scored_yet()
    {
        var board = Play([null], [null], holeCount: 18);

        Assert.Equal(0, board.HolesPlayed);
        Assert.Empty(board.Holes);
        Assert.False(board.IsDecided);
        Assert.Equal("No holes scored yet", board.Summary);
    }

    [Fact]
    public void Score_settles_a_three_way_game_only_on_an_outright_low()
    {
        var context = Context(
            GameType.Skins,
            [
                Line(1, new Dictionary<int, int?> { [1] = 3, [2] = 4 }, displayName: "Dale"),
                Line(2, new Dictionary<int, int?> { [1] = 4, [2] = 4 }, displayName: "Sam"),
                Line(3, new Dictionary<int, int?> { [1] = 4, [2] = 5 }, displayName: "Kit")
            ],
            holeCount: 2);

        var board = Engine.Score(context);

        Assert.Equal(1L, board.Holes[0].WonByParticipantId);
        Assert.True(board.Holes[1].IsTied);
        Assert.Equal(1, board.CarriedSkins);
    }
}
