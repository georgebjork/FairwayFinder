using FairwayFinder.Data;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Admin surface over rounds: view, edit, or soft-delete any user's round, and toggle
/// ExcludeFromStats. Reads, edits, and deletes reuse <see cref="IRoundService"/>; the exclude
/// toggle is the only direct DB write.
///
/// Editing goes through <see cref="UpdateRoundAsAdminAsync"/>, which resolves the round's owner
/// and submits the update under that identity. IRoundService.UpdateRoundAsync keeps its ownership
/// guard — this service never loosens it, it just supplies the correct owner.
/// </summary>
public class AdminRoundService(
    IRoundService roundService,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<AdminRoundService> logger)
{
    public Task<List<RoundResponse>> GetRoundsForUserAsync(string userId)
        => roundService.GetRoundsByUserIdAsync(userId);

    /// <summary>
    /// Every non-deleted round across all users, most recent first, with the owning player's
    /// name for display/filtering.
    /// </summary>
    public async Task<List<AdminRoundListItemDto>> GetAllRoundsAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var rounds = await db.Rounds
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.DatePlayed).ThenByDescending(r => r.RoundId)
            .Select(r => new
            {
                r.RoundId,
                r.UserId,
                r.DatePlayed,
                r.Score,
                r.FullRound,
                r.IsComplete,
                r.ExcludeFromStats,
                r.UsingShotTracking,
                r.UsingHoleStats,
                CourseName = r.Course.CourseName,
                TeeboxName = r.Teebox.TeeboxName,
                TeeboxPar = r.Teebox.Par,
                TeeboxIsNineHole = r.Teebox.IsNineHole
            })
            .ToListAsync();

        // Par of the holes each round actually has a score for. One grouped aggregate for the
        // whole grid, so to-par stays honest for rounds still being entered — and for completed
        // rounds that cover an unusual set of holes, which the teebox heuristic below also got
        // wrong.
        var playedPar = await db.Scores
            .Where(sc => !sc.IsDeleted)
            .Join(db.Holes.Where(h => !h.IsDeleted), sc => sc.HoleId, h => h.HoleId,
                (sc, h) => new { sc.RoundId, h.Par })
            .GroupBy(x => x.RoundId)
            .Select(g => new { RoundId = g.Key, Par = g.Sum(x => x.Par), Holes = g.Count() })
            .ToDictionaryAsync(x => x.RoundId, x => (x.Par, x.Holes));

        var userIds = rounds.Select(r => r.UserId).Distinct().ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync();
        var userMap = users.ToDictionary(u => u.Id);

        return rounds.Select(r =>
        {
            userMap.TryGetValue(r.UserId, out var u);
            var email = u?.Email ?? string.Empty;
            // Mirrors RoundResponse.ScoreToPar: the pars actually played when there are any,
            // falling back to the teebox only for a round with no scores on record.
            var hasPlayed = playedPar.TryGetValue(r.RoundId, out var played) && played.Par > 0;
            var par = hasPlayed
                ? played.Par
                : r.FullRound || r.TeeboxIsNineHole ? r.TeeboxPar : r.TeeboxPar / 2;

            return new AdminRoundListItemDto
            {
                RoundId = r.RoundId,
                UserId = r.UserId,
                PlayerName = DisplayNameHelper.BuildForAdmin(u?.FirstName, u?.LastName, email),
                PlayerEmail = email,
                DatePlayed = r.DatePlayed,
                Score = r.Score,
                ToPar = r.Score - par,
                CourseName = r.CourseName,
                TeeboxName = r.TeeboxName,
                ExcludeFromStats = r.ExcludeFromStats,
                UsingShotTracking = r.UsingShotTracking,
                UsingHoleStats = r.UsingHoleStats,
                FullRound = r.FullRound,
                IsComplete = r.IsComplete,
                HolesEntered = hasPlayed ? played.Holes : 0
            };
        }).ToList();
    }

    public Task<RoundResponse?> GetRoundDetailAsync(long roundId)
        => roundService.GetRoundByIdAsync(roundId);

    /// <summary>
    /// Soft-deletes a round and its children. DeleteRoundAsync has no owner guard (it matches
    /// on RoundId only), so this works cross-owner; <paramref name="adminUserId"/> is stamped
    /// as UpdatedBy on the deleted rows for audit.
    /// </summary>
    public Task<bool> DeleteRoundAsync(long roundId, string adminUserId)
        => roundService.DeleteRoundAsync(roundId, adminUserId);

    /// <summary>
    /// Toggles whether a round is excluded from stats. Single flag on Round — no child records.
    /// </summary>
    public async Task<bool> SetExcludeFromStatsAsync(long roundId, bool exclude, string adminUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var round = await db.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId && !r.IsDeleted);
        if (round is null) return false;

        round.ExcludeFromStats = exclude;
        round.UpdatedBy = adminUserId;
        round.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Fully loaded round for the admin detail page, with strokes gained computed at the given
    /// golfer level. GetRoundByIdAsync has no owner guard, so any round is readable.
    /// </summary>
    public Task<RoundResponse?> GetRoundForAdminAsync(long roundId, BaselineLevel level)
        => roundService.GetRoundByIdAsync(roundId, level);

    /// <summary>
    /// Returns the owning user's id for a round, or null when the round does not exist.
    /// </summary>
    public Task<string?> GetRoundOwnerIdAsync(long roundId)
        => roundService.GetRoundOwnerIdAsync(roundId);

    /// <summary>
    /// Updates a round on the owner's behalf. The caller-supplied <c>request.UserId</c> is ignored
    /// and replaced with the round's actual owner, so an admin can correct anyone's data without
    /// the update being rejected by the ownership guard and without the round changing hands.
    /// Returns false if the round does not exist.
    /// </summary>
    public async Task<bool> UpdateRoundAsAdminAsync(UpdateRoundRequest request, string adminUserId)
    {
        var ownerId = await roundService.GetRoundOwnerIdAsync(request.RoundId);
        if (ownerId is null)
        {
            logger.LogWarning(
                "Admin {AdminUserId} attempted to edit round {RoundId}, which does not exist.",
                adminUserId, request.RoundId);
            return false;
        }

        request.UserId = ownerId;

        var updated = await roundService.UpdateRoundAsync(request);

        if (updated)
        {
            logger.LogInformation(
                "Admin {AdminUserId} edited round {RoundId} owned by {OwnerUserId}.",
                adminUserId, request.RoundId, ownerId);
        }

        return updated;
    }
}
