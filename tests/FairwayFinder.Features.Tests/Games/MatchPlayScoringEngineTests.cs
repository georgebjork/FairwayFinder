using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using static FairwayFinder.Features.Tests.Helpers.GameScoringTestData;

namespace FairwayFinder.Features.Tests.Games;

public class MatchPlayScoringEngineTests
{
    private static readonly MatchPlayScoringEngine Engine = new();

    private static Dictionary<int, int?> Holes(IReadOnlyList<int?> scores)
        => scores.Select((s, i) => (Hole: i + 1, Score: s)).ToDictionary(x => x.Hole, x => x.Score);

    /// <summary>A singles match over <paramref name="holeCount"/> holes.</summary>
    private static MatchPlayScoreboard Play(
        IReadOnlyList<int?> dale,
        IReadOnlyList<int?> sam,
        int holeCount = 18,
        bool useNet = false,
        IReadOnlyDictionary<int, int>? daleStrokes = null,
        IReadOnlyDictionary<int, int>? samStrokes = null)
    {
        var context = Context(
            GameType.MatchPlay,
            [
                Line(1, Holes(dale), displayName: "Dale", strokesReceived: daleStrokes),
                Line(2, Holes(sam), displayName: "Sam", strokesReceived: samStrokes)
            ],
            holeCount,
            Rules(useNet: useNet));

        return Engine.Score(context);
    }

    /// <summary>Scores that win the first <paramref name="wins"/> holes then halve the rest.</summary>
    private static (List<int?> Winner, List<int?> Loser) WinFirst(int wins, int through)
    {
        var winner = new List<int?>();
        var loser = new List<int?>();

        for (var n = 1; n <= through; n++)
        {
            winner.Add(4);
            loser.Add(n <= wins ? 5 : 4);
        }

        return (winner, loser);
    }

    [Fact]
    public void Score_halves_a_hole_the_two_sides_tie()
    {
        var board = Play([4], [4], holeCount: 18);

        Assert.True(board.Holes[0].IsHalved);
        Assert.Null(board.Holes[0].WonBySideParticipantId);
        Assert.Equal(0, board.HolesUp);
        Assert.Null(board.LeaderParticipantId);
        Assert.Equal("AS", board.ResultLine);
    }

    [Fact]
    public void Score_awards_a_hole_to_the_lower_gross_score()
    {
        var board = Play([4, 4], [5, 3], holeCount: 18);

        Assert.Equal(1L, board.Holes[0].WonBySideParticipantId);
        Assert.Equal(2L, board.Holes[1].WonBySideParticipantId);
        Assert.Equal(0, board.HolesUp);
    }

    [Fact]
    public void Score_flips_a_hole_when_a_stroke_makes_the_net_lower()
    {
        // Gross 5 vs 4 is Sam's hole. With a shot, Dale's net 4 halves it.
        var strokes = new Dictionary<int, int> { [1] = 1 };

        var gross = Play([5], [4], holeCount: 18, useNet: false, daleStrokes: strokes);
        Assert.Equal(2L, gross.Holes[0].WonBySideParticipantId);

        var net = Play([5], [4], holeCount: 18, useNet: true, daleStrokes: strokes);
        Assert.True(net.Holes[0].IsHalved);
    }

    [Fact]
    public void Score_reports_dormie_when_the_lead_equals_the_holes_left()
    {
        // 2 up with 2 to play over 18 holes: win the first two, halve through 16.
        var (winner, loser) = WinFirst(wins: 2, through: 16);
        var board = Play(winner, loser, holeCount: 18);

        Assert.True(board.IsDormie);
        Assert.False(board.IsDecided);
        Assert.Equal(2, board.HolesUp);
        Assert.Equal(2, board.HolesRemaining);
        Assert.Contains("dormie", board.Summary);
    }

    [Fact]
    public void Score_does_not_report_dormie_on_a_finished_match()
    {
        // All square after 18: HolesUp and HolesRemaining are both zero, which must not read
        // as dormie.
        var board = Play([.. Enumerable.Repeat((int?)4, 18)], [.. Enumerable.Repeat((int?)4, 18)], holeCount: 18);

        Assert.False(board.IsDormie);
        Assert.True(board.IsDecided);
    }

    [Fact]
    public void Score_closes_the_match_out_four_and_three()
    {
        // 4 up after 15 holes with 3 to play.
        var (winner, loser) = WinFirst(wins: 4, through: 15);
        var board = Play(winner, loser, holeCount: 18);

        Assert.True(board.IsDecided);
        Assert.Equal(15, board.DecidedOnHole);
        Assert.Equal("4 & 3", board.ResultLine);
        Assert.Equal(4, board.HolesUp);
        Assert.Equal(1L, board.LeaderParticipantId);
    }

    [Fact]
    public void Score_keeps_the_four_and_three_result_when_the_last_holes_are_played_out()
    {
        // Regression guard: groups play 16, 17, 18 out because a skins game rides on the same
        // round. Losing all three after closing out 4 & 3 must not rewrite the match as 1 UP.
        var (winner, loser) = WinFirst(wins: 4, through: 15);
        winner.AddRange([5, 5, 5]);
        loser.AddRange([4, 4, 4]);

        var board = Play(winner, loser, holeCount: 18);

        Assert.Equal("4 & 3", board.ResultLine);
        Assert.Equal(4, board.HolesUp);
        Assert.Equal(15, board.DecidedOnHole);
        Assert.Equal(1L, board.LeaderParticipantId);
        Assert.True(board.IsDecided);

        // The card still carries all 18 holes so the client can render it.
        Assert.Equal(18, board.Holes.Count);
        Assert.Equal(2L, board.Holes[17].WonBySideParticipantId);
    }

    [Fact]
    public void Score_reports_one_up_when_the_match_is_won_on_the_last_hole()
    {
        var (winner, loser) = WinFirst(wins: 1, through: 18);
        var board = Play(winner, loser, holeCount: 18);

        Assert.True(board.IsDecided);
        Assert.Equal("1 UP", board.ResultLine);
        Assert.Equal(1, board.HolesUp);
    }

    [Fact]
    public void Score_reports_all_square_after_eighteen()
    {
        var board = Play([.. Enumerable.Repeat((int?)4, 18)], [.. Enumerable.Repeat((int?)4, 18)], holeCount: 18);

        Assert.Equal("AS", board.ResultLine);
        Assert.Null(board.LeaderParticipantId);
        Assert.True(board.IsDecided);
        Assert.Equal("Match halved", board.Summary);
    }

    [Fact]
    public void Score_ignores_holes_after_the_first_gap()
    {
        // Sam has not entered hole 3. Holes 4+ are not settled, so the match reads 2 up thru 2.
        var dale = new List<int?> { 4, 4, 4, 4 };
        var sam = new List<int?> { 5, 5, null, 5 };

        var board = Play(dale, sam, holeCount: 18);

        Assert.Equal(2, board.HolesPlayed);
        Assert.Equal(16, board.HolesRemaining);
        Assert.Equal(2, board.Holes.Count);
        Assert.Equal(2, board.HolesUp);
        Assert.False(board.IsDecided);
    }

    [Fact]
    public void Score_scores_a_team_side_on_its_best_ball()
    {
        // Team 1: 6 and 3 -> best ball 3. Team 2: 4 and 4 -> best ball 4. Team 1 wins the hole.
        var context = Context(
            GameType.MatchPlay,
            [
                Line(1, new Dictionary<int, int?> { [1] = 6 }, displayName: "Dale", team: 1),
                Line(2, new Dictionary<int, int?> { [1] = 3 }, displayName: "Kit", team: 1),
                Line(3, new Dictionary<int, int?> { [1] = 4 }, displayName: "Sam", team: 2),
                Line(4, new Dictionary<int, int?> { [1] = 4 }, displayName: "Ana", team: 2)
            ],
            holeCount: 18,
            Rules());

        var board = Engine.Score(context);

        Assert.Equal(1L, board.Holes[0].WonBySideParticipantId);
        Assert.Equal(1, board.HolesUp);

        // The card names both members of each side, and reports the ball that counted.
        var winningSide = board.Holes[0].Sides[0];
        Assert.Equal([1L, 2L], winningSide.ParticipantIds);
        Assert.Equal("Dale / Kit", winningSide.DisplayName);
        Assert.Equal(3, winningSide.Strokes);
    }

    [Fact]
    public void Score_tracks_the_running_leader_hole_by_hole()
    {
        var board = Play([4, 5, 4], [5, 3, 5], holeCount: 18);

        Assert.Equal(1L, board.Holes[0].RunningLeaderParticipantId);
        Assert.Equal(1, board.Holes[0].RunningHolesUp);
        Assert.Null(board.Holes[1].RunningLeaderParticipantId);
        Assert.Equal(0, board.Holes[1].RunningHolesUp);
        Assert.Equal(1L, board.Holes[2].RunningLeaderParticipantId);
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
    public void Score_refuses_a_field_that_is_not_two_sides()
    {
        var context = Context(
            GameType.MatchPlay,
            [
                Line(1, AtPar(), displayName: "Dale"),
                Line(2, AtPar(), displayName: "Sam"),
                Line(3, AtPar(), displayName: "Kit")
            ],
            holeCount: 18);

        var board = Engine.Score(context);

        Assert.Empty(board.Holes);
        Assert.Contains("two sides", board.Summary);
    }

    [Fact]
    public void Score_closes_out_a_nine_hole_match()
    {
        // 3 up after 7 with 2 to play on a nine-hole game.
        var (winner, loser) = WinFirst(wins: 3, through: 7);
        var board = Play(winner, loser, holeCount: 9);

        Assert.True(board.IsDecided);
        Assert.Equal("3 & 2", board.ResultLine);
        Assert.Equal(7, board.DecidedOnHole);
    }

    [Fact]
    public void Score_treats_a_lead_equal_to_the_holes_left_as_dormie_not_a_win()
    {
        // 3 up with 3 to play is dormie: the trailing side can still halve the match.
        var (winner, loser) = WinFirst(wins: 3, through: 6);
        var board = Play(winner, loser, holeCount: 9);

        Assert.False(board.IsDecided);
        Assert.True(board.IsDormie);
        Assert.Null(board.DecidedOnHole);
    }

    [Fact]
    public void Score_puts_the_leader_first_in_the_standings()
    {
        var (winner, loser) = WinFirst(wins: 2, through: 4);
        var board = Play(winner, loser, holeCount: 18);

        var lead = board.Standings.Single(s => s.IsLeader);
        Assert.Equal(1L, lead.ParticipantId);
        Assert.Equal("2 UP", lead.Value);
        Assert.Equal("2 DN", board.Standings.Single(s => !s.IsLeader).Value);
    }
}
