using FairwayFinder.Data.Entities;

namespace FairwayFinder.Features.Games;

public interface IGameScoringEngineResolver
{
    /// <summary>The engine for a game type, or <see cref="NotSupportedException"/> if none is registered.</summary>
    IGameScoringEngine For(GameType gameType);

    /// <summary>
    /// Whether a game type can be scored at all. Checked before a game is created so an
    /// unscoreable game never reaches the database, where it would 500 on every poll.
    /// </summary>
    bool Supports(GameType gameType);
}

public sealed class GameScoringEngineResolver : IGameScoringEngineResolver
{
    private readonly Dictionary<GameType, IGameScoringEngine> _engines;

    public GameScoringEngineResolver(IEnumerable<IGameScoringEngine> engines)
        => _engines = engines.ToDictionary(e => e.GameType);

    public IGameScoringEngine For(GameType gameType)
        => _engines.TryGetValue(gameType, out var engine)
            ? engine
            : throw new NotSupportedException($"No scoring engine registered for {gameType}.");

    public bool Supports(GameType gameType) => _engines.ContainsKey(gameType);
}
