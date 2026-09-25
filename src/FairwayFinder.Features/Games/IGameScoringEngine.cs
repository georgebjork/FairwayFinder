using FairwayFinder.Data.Entities;

namespace FairwayFinder.Features.Games;

/// <summary>
/// One game's scoring rules. Implementations are pure functions over a
/// <see cref="GameScoringContext"/>: no database, no clock, no state. That is what makes them
/// trivially testable and what lets the same engine score a live game and a finished one.
/// </summary>
public interface IGameScoringEngine
{
    GameType GameType { get; }

    GameScoreboard Score(GameScoringContext context);
}

/// <summary>
/// The typed face of <see cref="IGameScoringEngine"/>. Engines implement this so their own tests
/// and any direct caller get the concrete scoreboard back; the resolver holds the non-generic
/// form so every game type can live in one dictionary.
/// </summary>
public interface IGameScoringEngine<out TScoreboard> : IGameScoringEngine
    where TScoreboard : GameScoreboard
{
    new TScoreboard Score(GameScoringContext context);
}

/// <summary>Bridges the two so an engine only writes <c>Score</c> once.</summary>
public abstract class GameScoringEngine<TScoreboard> : IGameScoringEngine<TScoreboard>
    where TScoreboard : GameScoreboard
{
    public abstract GameType GameType { get; }

    public abstract TScoreboard Score(GameScoringContext context);

    GameScoreboard IGameScoringEngine.Score(GameScoringContext context) => Score(context);
}
