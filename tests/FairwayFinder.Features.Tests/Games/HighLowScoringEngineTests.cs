using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using static FairwayFinder.Features.Tests.Helpers.GameScoringTestData;

namespace FairwayFinder.Features.Tests.Games;

public class HighLowScoringEngineTests
{
    private static readonly HighLowScoringEngine Engine = new();

    private static Dictionary<int, int?> ToHoles(IReadOnlyList<int?> scores)
        => scores.Select((s, i) => (Hole: i + 1, Score: s)).ToDictionary(x => x.Hole, x => x.Score);

    /// <summary>
    /// Dale (1) and Kit (2) on team 1 against Sam (3) and Ana (4) on team 2, each player's scores
    /// given hole by hole.
    /// </summary>
    private static HighLowScoreboard Play(
        IReadOnlyList<int?> dale,
        IReadOnlyList<int?> kit,
        IReadOnlyList<int?> sam,
        IReadOnlyList<int?> ana,
        int holeCount = 18,
        bool useNet = false,
        IReadOnlyDictionary<int, int>? daleStrokes = null)
    {
        var context = Context(
            GameType.HighLow,
            [
                Line(1, ToHoles(dale), displayName: "Dale", team: 1, strokesReceived: daleStrokes),
                Line(2, ToHoles(kit), displayName: "Kit", team: 1),
                Line(3, ToHoles(sam), displayName: "Sam", team: 2),
                Line(4, ToHoles(ana), displayName: "Ana", team: 2)
            ],
            holeCount,
            Rules(useNet: useNet));

        return Engine.Score(context);
    }

    private static int PointsOf(HighLowScoreboard board, long sideKey)
        => board.Sides.Single(s => s.SideKeyParticipantId == sideKey).Points;

    [Fact]
    public void Score_gives_both_points_to_a_side_with_the_better_low_and_high()
    {
        // Team 1: 3 and 4. Team 2: 4 and 5. Low 3 < 4, high 4 < 5.
        var board = Play([3], [4], [4], [5], holeCount: 1);

        Assert.Equal(2, PointsOf(board, 1));
        Assert.Equal(0, PointsOf(board, 3));
        Assert.Equal(1L, board.Holes[0].LowWonBySideParticipantId);
        Assert.Equal(1L, board.Holes[0].HighWonBySideParticipantId);
    }

    [Fact]
    public void Score_splits_the_hole_when_each_side_takes_one_point()
    {
        // Team 1: 3 and 7 — best low, worst high. Team 2: 4 and 5.
        var board = Play([3], [7], [4], [5], holeCount: 1);

        Assert.Equal(1, PointsOf(board, 1));
        Assert.Equal(1, PointsOf(board, 3));
        Assert.Equal(1L, board.Holes[0].LowWonBySideParticipantId);
        Assert.Equal(3L, board.Holes[0].HighWonBySideParticipantId);
    }

    [Fact]
    public void Score_voids_a_tied_low_and_still_awards_the_high()
    {
        // Lows tie at 4; team 2's high (5) beats team 1's (6).
        var board = Play([4], [6], [4], [5], holeCount: 1);

        Assert.Null(board.Holes[0].LowWonBySideParticipantId);
        Assert.Equal(3L, board.Holes[0].HighWonBySideParticipantId);
        Assert.Equal(0, PointsOf(board, 1));
        Assert.Equal(1, PointsOf(board, 3));
    }

    [Fact]
    public void Score_awards_nothing_when_both_points_tie()
    {
        var board = Play([4], [5], [5], [4], holeCount: 1);

        Assert.Null(board.Holes[0].LowWonBySideParticipantId);
        Assert.Null(board.Holes[0].HighWonBySideParticipantId);
        Assert.Equal(0, PointsOf(board, 1));
        Assert.Equal(0, PointsOf(board, 3));
    }

    [Fact]
    public void Score_settles_on_net_when_the_game_plays_net()
    {
        // Gross: team 1's high (6) loses to team 2's (5). Dale gets a stroke, so his net 5 ties it.
        var gross = Play([6], [4], [4], [5], holeCount: 1);
        var net = Play([6], [4], [4], [5], holeCount: 1, useNet: true, daleStrokes: new Dictionary<int, int> { [1] = 1 });

        Assert.Equal(3L, gross.Holes[0].HighWonBySideParticipantId);
        Assert.Null(net.Holes[0].HighWonBySideParticipantId);
    }

    [Fact]
    public void Score_stops_at_the_first_unsettled_hole()
    {
        // Ana has not entered hole 2, so hole 3 is not settled either.
        var board = Play([3, 3, 3], [4, 4, 4], [5, 5, 5], [5, null, 5], holeCount: 3);

        Assert.Equal(1, board.HolesPlayed);
        Assert.Equal(2, board.HolesRemaining);
        Assert.Single(board.Holes);
        Assert.Equal(2, PointsOf(board, 1));
    }

    [Fact]
    public void Score_is_decided_once_the_lead_exceeds_every_point_left()
    {
        // Team 1 sweeps 7 holes of 9: 14 points up with 2 holes (4 points) left.
        var low = Enumerable.Repeat<int?>(3, 7).ToList();
        var high = Enumerable.Repeat<int?>(6, 7).ToList();

        var board = Play(low, low, high, high, holeCount: 9);

        Assert.True(board.IsDecided);
        Assert.Equal(14, board.Margin);
        Assert.Equal(1L, board.LeaderParticipantId);
        Assert.Equal("Dale / Kit win 14–0", board.Summary);
    }

    [Fact]
    public void Score_is_not_decided_while_the_trailing_side_can_still_catch_up()
    {
        var board = Play([3], [3], [6], [6], holeCount: 2);

        Assert.False(board.IsDecided);
        Assert.Equal("Dale / Kit lead by 2 thru 1 (2–0)", board.Summary);
    }

    [Fact]
    public void Score_reports_a_halved_match_when_the_points_finish_level()
    {
        // Hole 1 to team 1, hole 2 to team 2.
        var board = Play([3, 6], [3, 6], [6, 3], [6, 3], holeCount: 2);

        Assert.True(board.IsDecided);
        Assert.Null(board.LeaderParticipantId);
        Assert.Equal("Match halved 2–2", board.Summary);
        Assert.All(board.Standings, s => Assert.Equal(1, s.Position));
        Assert.All(board.Standings, s => Assert.False(s.IsLeader));
    }

    [Fact]
    public void Score_ranks_the_leading_side_first_in_the_standings()
    {
        var board = Play([5], [6], [3], [4], holeCount: 1);

        Assert.Equal(3L, board.Standings[0].ParticipantId);
        Assert.Equal("Sam / Ana", board.Standings[0].DisplayName);
        Assert.Equal("2 pts", board.Standings[0].Value);
        Assert.True(board.Standings[0].IsLeader);
        Assert.Equal(2, board.Standings[1].Position);
        Assert.Equal("0 pts", board.Standings[1].Value);
    }

    [Fact]
    public void Score_reports_who_made_the_low_and_high_ball()
    {
        var board = Play([5], [3], [4], [6], holeCount: 1);

        var teamOne = board.Holes[0].Sides.Single(s => s.SideKeyParticipantId == 1);
        Assert.Equal(3, teamOne.Low);
        Assert.Equal(2L, teamOne.LowParticipantId);
        Assert.Equal(5, teamOne.High);
        Assert.Equal(1L, teamOne.HighParticipantId);

        var teamTwo = board.Holes[0].Sides.Single(s => s.SideKeyParticipantId == 3);
        Assert.Equal(4L, teamTwo.HighParticipantId);
    }

    [Fact]
    public void Score_returns_an_empty_board_for_individuals()
    {
        var context = Context(
            GameType.HighLow,
            [Line(1, Flat(4, 3)), Line(2, Flat(5, 3))],
            holeCount: 3);

        var board = Engine.Score(context);

        Assert.Empty(board.Standings);
        Assert.Empty(board.Holes);
        Assert.False(board.IsDecided);
    }

    [Fact]
    public void Score_returns_an_empty_board_for_uneven_teams()
    {
        var context = Context(
            GameType.HighLow,
            [
                Line(1, Flat(4, 3), team: 1),
                Line(2, Flat(4, 3), team: 1),
                Line(3, Flat(4, 3), team: 1),
                Line(4, Flat(5, 3), team: 2)
            ],
            holeCount: 3);

        var board = Engine.Score(context);

        Assert.Empty(board.Sides);
        Assert.Contains("two equal teams", board.Summary);
    }

    [Fact]
    public void Scoreboard_round_trips_through_the_polymorphic_base()
    {
        var board = Play([3], [4], [4], [5], holeCount: 1);

        var json = System.Text.Json.JsonSerializer.Serialize<GameScoreboard>(board);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameScoreboard>(json);

        Assert.Contains("\"gameType\":\"HighLow\"", json);
        var typed = Assert.IsType<HighLowScoreboard>(back);
        Assert.Equal(2, typed.Sides.Single(s => s.SideKeyParticipantId == 1).Points);
    }
}
