using FairwayFinder.Features.Data;
using FairwayFinder.Features.Games;

namespace FairwayFinder.Features.Services.Interfaces;

/// <summary>
/// Games: a side contest laid over the rounds people are already entering. Every mutating call
/// returns the recomputed <see cref="GameStateResponse"/>, so the app never needs a follow-up GET
/// after writing — the same courtesy the round-entry flow gives.
/// </summary>
public interface IGameService
{
    Task<GameResult<GameStateResponse>> CreateGameAsync(CreateGameRequest request, string hostUserId);

    Task<List<GameSummaryResponse>> GetMyGamesAsync(string userId, bool activeOnly);

    /// <summary>
    /// The poll endpoint's read. A completed game serves its stored snapshot rather than
    /// recomputing, so editing a linked round afterwards cannot move a settled bet.
    /// </summary>
    /// <param name="userId">
    /// The caller, who must be a participant. Null means a trusted caller — the admin console —
    /// and skips that check. Never pass null from the API.
    /// </param>
    Task<GameResult<GameStateResponse>> GetGameAsync(long gameId, string? userId);

    /// <summary>
    /// Resolves a join code to the game it names, without joining. Joining requires a teebox on
    /// the game's course, which the holder of a bare code has no way to pick — so the app previews
    /// first, then joins. Any authenticated caller may preview: the code is the credential.
    /// </summary>
    Task<GameResult<GameJoinPreviewResponse>> PreviewGameAsync(string joinCode, string userId);

    Task<GameResult<GameStateResponse>> JoinGameAsync(JoinGameRequest request, string userId);

    Task<GameResult<GameStateResponse>> AddParticipantAsync(long gameId, AddParticipantRequest request, string hostUserId);

    Task<GameResult<GameStateResponse>> UpdateParticipantAsync(long gameId, long participantId, UpdateParticipantRequest request, string userId);

    Task<GameResult<GameStateResponse>> RemoveParticipantAsync(long gameId, long participantId, string hostUserId);

    /// <summary>Links the caller's own in-progress round, or unlinks when the round id is null.</summary>
    Task<GameResult<GameStateResponse>> LinkRoundAsync(long gameId, long? roundId, string userId);

    Task<GameResult<GameStateResponse>> UpsertParticipantHoleAsync(long gameId, long participantId, int holeNumber, UpsertGameHoleRequest request, string userId);

    Task<GameResult<GameStateResponse>> ClearParticipantHoleAsync(long gameId, long participantId, int holeNumber, string userId);

    Task<GameResult<GameStateResponse>> StartGameAsync(long gameId, string hostUserId);

    Task<GameResult<GameStateResponse>> CompleteGameAsync(long gameId, string hostUserId);

    Task<GameResult<GameStateResponse>> AbandonGameAsync(long gameId, string hostUserId);
}
