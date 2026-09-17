using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Games.Engines;
using FairwayFinder.Features.Tests.Helpers;

namespace FairwayFinder.Features.Tests.Games;

/// <summary>
/// Proves the engine contract — the dictionary, the generic bridge, and the DI shape — without
/// any golf in it. A fake engine is enough; the real ones are tested on their own rules.
/// </summary>
public class GameScoringEngineResolverTests
{
    private sealed class FakeScoringEngine(GameType gameType) : GameScoringEngine<SkinsScoreboard>
    {
        public override GameType GameType { get; } = gameType;

        public override SkinsScoreboard Score(GameScoringContext context) => new()
        {
            HolesPlayed = 0,
            HolesRemaining = 0,
            IsDecided = true,
            Summary = "fake",
            Standings = [],
            CarriedSkins = 0,
            Holes = [],
            Tallies = []
        };
    }

    [Fact]
    public void For_returns_the_engine_registered_for_a_game_type()
    {
        var engine = new FakeScoringEngine(GameType.Skins);
        var resolver = new GameScoringEngineResolver([engine]);

        Assert.Same(engine, resolver.For(GameType.Skins));
    }

    [Fact]
    public void For_throws_for_a_game_type_with_no_engine()
    {
        var resolver = new GameScoringEngineResolver([new FakeScoringEngine(GameType.Skins)]);

        var ex = Assert.Throws<NotSupportedException>(() => resolver.For(GameType.MatchPlay));
        Assert.Contains("MatchPlay", ex.Message);
    }

    [Fact]
    public void Supports_reports_what_is_registered()
    {
        var resolver = new GameScoringEngineResolver([new FakeScoringEngine(GameType.Skins)]);

        Assert.True(resolver.Supports(GameType.Skins));
        Assert.False(resolver.Supports(GameType.MatchPlay));
    }

    [Fact]
    public void The_generic_bridge_routes_the_non_generic_call_to_the_typed_one()
    {
        IGameScoringEngine engine = new FakeScoringEngine(GameType.Skins);
        var context = GameScoringTestData.Context(GameType.Skins, [
            GameScoringTestData.Line(1, GameScoringTestData.AtPar())
        ]);

        var board = engine.Score(context);

        Assert.IsType<SkinsScoreboard>(board);
        Assert.Equal("fake", board.Summary);
    }

    [Fact]
    public void Both_shipped_engines_resolve_when_registered_together()
    {
        var resolver = new GameScoringEngineResolver([
            new SkinsScoringEngine(),
            new MatchPlayScoringEngine()
        ]);

        Assert.True(resolver.Supports(GameType.Skins));
        Assert.True(resolver.Supports(GameType.MatchPlay));
        Assert.Equal(GameType.Skins, resolver.For(GameType.Skins).GameType);
        Assert.Equal(GameType.MatchPlay, resolver.For(GameType.MatchPlay).GameType);
    }
}
