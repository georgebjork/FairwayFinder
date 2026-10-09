using FairwayFinder.Admin.Components.Pages.Games.Components;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Games;

namespace FairwayFinder.Admin.IntegrationTests.Pages;

/// <summary>
/// The game scorecard renders whatever <see cref="GameCardModel"/> says, so this is where "who won
/// the hole" is decided for the admin console. Pure mapping over hand-built scoreboards — no host,
/// no database.
/// </summary>
public class GameCardModelTests
{
    [Fact]
    public void Four_ball_match_lights_only_the_ball_that_counted()
    {
        // Team 1 (1, 2) beats team 2 (3, 4) on hole 1 with player 2's 3.
        var state = State(
            GameType.MatchPlay,
            [Player(1, "Dale Smith", team: 1), Player(2, "Sam Jones", team: 1), Player(3, "Ann Lee", team: 2), Player(4, "Bo Park", team: 2)],
            new MatchPlayScoreboard
            {
                HolesPlayed = 1, HolesRemaining = 17, IsDecided = false, Summary = "", Standings = [],
                LeaderParticipantId = 1, HolesUp = 1, IsDormie = false, DecidedOnHole = null, ResultLine = "1 UP",
                Holes = [new MatchPlayHole(1, 4, [], WonBySideParticipantId: 1, IsHalved: false, RunningLeaderParticipantId: 1, RunningHolesUp: 1)]
            });
        var lines = Lines((1, 1, 5), (2, 1, 3), (3, 1, 4), (4, 1, 4));

        var model = GameCardModel.From(state, lines);
        var hole = model.Hole(1)!;

        Assert.Equal([2L], hole.Awards.Keys);
        Assert.Equal("T1", hole.Result.Text);
        Assert.Equal("1 UP", hole.Standing!.Text);
        Assert.Equal(model.SideOf(1)!.Index, hole.Standing.SideIndex);
    }

    [Fact]
    public void Match_play_fades_holes_after_the_close_out()
    {
        var state = State(
            GameType.MatchPlay,
            [Player(1, "Dale"), Player(2, "Sam")],
            new MatchPlayScoreboard
            {
                HolesPlayed = 2, HolesRemaining = 0, IsDecided = true, Summary = "", Standings = [],
                LeaderParticipantId = 1, HolesUp = 1, IsDormie = false, DecidedOnHole = 1, ResultLine = "1 UP",
                Holes =
                [
                    new MatchPlayHole(1, 4, [], 1, false, 1, 1),
                    new MatchPlayHole(2, 4, [], null, true, 1, 1)
                ]
            });

        var model = GameCardModel.From(state, Lines((1, 1, 4), (2, 1, 5), (1, 2, 4), (2, 2, 4)));

        Assert.False(model.Hole(1)!.AfterDecided);
        Assert.True(model.Hole(2)!.AfterDecided);
        Assert.Equal("½", model.Hole(2)!.Result.Text);
        Assert.Empty(model.Hole(2)!.Awards);
    }

    [Fact]
    public void Skins_tags_a_carried_win_and_labels_a_tie_as_a_carry()
    {
        var state = State(
            GameType.Skins,
            [Player(1, "Dale"), Player(2, "Sam")],
            new SkinsScoreboard
            {
                HolesPlayed = 2, HolesRemaining = 16, IsDecided = false, Summary = "", Standings = [],
                CarriedSkins = 0, Tallies = [],
                Holes =
                [
                    new SkinsHole(1, 4, null, 0, 0, IsTied: true),
                    new SkinsHole(2, 4, 2, 2, 1, IsTied: false)
                ]
            },
            skinsCarryover: true);

        var model = GameCardModel.From(state, Lines());

        Assert.Empty(model.Sides);
        Assert.Equal("Carry", model.Hole(1)!.Result.Text);
        Assert.Equal("×2", model.Hole(2)!.Awards[2]);
        Assert.True(model.Hole(2)!.Result.IsWin);
        Assert.Null(model.StandingLabel);
    }

    [Fact]
    public void High_low_tags_the_golfers_who_made_the_low_and_high_balls()
    {
        var state = State(
            GameType.HighLow,
            [Player(1, "A", team: 1), Player(2, "B", team: 1), Player(3, "C", team: 2), Player(4, "D", team: 2)],
            new HighLowScoreboard
            {
                HolesPlayed = 1, HolesRemaining = 17, IsDecided = false, Summary = "", Standings = [],
                LeaderParticipantId = 1, Margin = 1, Sides = [],
                Holes =
                [
                    new HighLowHole(1, 4,
                        [
                            new HighLowHoleSide(1, [1, 2], "A / B", Low: 3, LowParticipantId: 1, High: 6, HighParticipantId: 2, Points: 1),
                            new HighLowHoleSide(3, [3, 4], "C / D", Low: 4, LowParticipantId: 3, High: 5, HighParticipantId: 4, Points: 1)
                        ],
                        LowWonBySideParticipantId: 1,
                        HighWonBySideParticipantId: 3,
                        RunningLeaderParticipantId: null,
                        RunningMargin: 0)
                ]
            });

        var model = GameCardModel.From(state, Lines());
        var hole = model.Hole(1)!;

        Assert.Equal("L", hole.Awards[1]);
        Assert.Equal("H", hole.Awards[4]);
        Assert.Equal(2, hole.Awards.Count);
        Assert.Equal("1–1", hole.Result.Text);
        Assert.Equal("AS", hole.Standing!.Text);
    }

    [Fact]
    public void A_game_still_in_setup_has_sides_but_no_hole_results()
    {
        var state = State(GameType.MatchPlay, [Player(1, "Dale"), Player(2, "Sam")], scoreboard: null);

        var model = GameCardModel.From(state, Lines());

        Assert.Equal(2, model.Sides.Count);
        Assert.Empty(model.Holes);
    }

    // ── Builders ──

    private static GameStateResponse State(
        GameType type,
        List<GameParticipantResponse> participants,
        GameScoreboard? scoreboard,
        bool skinsCarryover = false) => new()
    {
        GameType = type,
        State = GameState.Active,
        Rules = new GameRules(UseNet: false, 100, StrokesOffLow: false, skinsCarryover, SkinsValue: null),
        HoleNumbers = [.. Enumerable.Range(1, 18)],
        Participants = participants,
        Scoreboard = scoreboard
    };

    private static GameParticipantResponse Player(long id, string name, int? team = null)
        => new() { ParticipantId = id, DisplayName = name, Team = team };

    private static IReadOnlyDictionary<long, IReadOnlyDictionary<int, GameHoleLine>> Lines(
        params (long ParticipantId, int Hole, int Strokes)[] scores)
        => scores
            .GroupBy(s => s.ParticipantId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<int, GameHoleLine>)g.ToDictionary(
                    s => s.Hole,
                    s => new GameHoleLine(s.Hole, Par: 4, StrokeIndex: s.Hole, s.Strokes, StrokesReceived: 0)));
}
