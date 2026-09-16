using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;

namespace FairwayFinder.Features.Services.Interfaces;

/// <summary>
/// The hole-by-hole round lifecycle: start, write holes as they are played, post.
/// <para>
/// Separate from <see cref="IRoundService"/>, which owns reading rounds and the atomic
/// whole-round submit. Live scoring has a different shape — incremental writes, two devices on
/// one round, a round that is not history until it is posted — and <c>RoundService</c> is
/// already large. The scoring arithmetic both paths need lives in
/// <c>RoundScoringHelper</c> rather than in either service, so the two cannot drift.
/// </para>
/// </summary>
public interface IRoundEntryService
{
    /// <summary>
    /// Opens an empty round for hole-by-hole entry. Writes no scores, builds no round stats, and
    /// notifies nobody — a round only becomes news when it is posted. Fails with
    /// <see cref="RoundEntryStatus.ActiveRoundExists"/> if the golfer already has one open.
    /// </summary>
    Task<RoundEntryResult<StartRoundResponse>> StartRoundAsync(StartRoundRequest request, string userId);

    /// <summary>
    /// The golfer's round in progress, or null. This is the resume path, and the only read that
    /// returns an unposted round to its owner.
    /// </summary>
    Task<RoundResponse?> GetActiveRoundAsync(string userId, BaselineLevel level);

    /// <summary>
    /// Writes one hole. Idempotent: posting the same hole again overwrites it rather than adding
    /// a second, so a retry over patchy course signal is safe. The hole's id and par come from
    /// the round's teebox, not the caller.
    /// </summary>
    Task<RoundEntryResult<RoundProgressResponse>> UpsertHoleAsync(
        long roundId, int holeNumber, UpsertHoleRequest request, string userId);

    /// <summary>
    /// Removes a hole's score, stats, and shots. Succeeds whether or not the hole had a score, so
    /// the client can clear freely.
    /// </summary>
    Task<RoundEntryResult<RoundProgressResponse>> ClearHoleAsync(long roundId, int holeNumber, string userId);

    /// <summary>
    /// Posts the round: recomputes totals, builds the scoring distribution, derives the shape
    /// flags from the holes actually entered, flips <c>IsComplete</c>, and notifies friends.
    /// Requires a complete eighteen or nine — otherwise fails with
    /// <see cref="RoundEntryStatus.RoundIncomplete"/> and the missing hole numbers.
    /// Idempotent: posting an already-posted round returns it without notifying again.
    /// </summary>
    Task<RoundEntryResult<RoundResponse>> CompleteRoundAsync(long roundId, string userId, BaselineLevel level);
}
