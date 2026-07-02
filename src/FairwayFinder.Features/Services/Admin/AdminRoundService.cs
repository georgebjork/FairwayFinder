using FairwayFinder.Data;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Thin admin surface over rounds: view any user's rounds, soft-delete a round, and toggle
/// ExcludeFromStats. Intentionally does NOT support editing scores/shots. Reads and delete
/// reuse <see cref="IRoundService"/>; the exclude toggle is the only direct DB write.
/// </summary>
public class AdminRoundService(
    IRoundService roundService,
    IDbContextFactory<ApplicationDbContext> dbContextFactory)
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
                r.ExcludeFromStats,
                r.UsingShotTracking,
                r.UsingHoleStats,
                CourseName = r.Course.CourseName,
                TeeboxName = r.Teebox.TeeboxName,
                TeeboxPar = r.Teebox.Par,
                TeeboxIsNineHole = r.Teebox.IsNineHole
            })
            .ToListAsync();

        var userIds = rounds.Select(r => r.UserId).Distinct().ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync();
        var userMap = users.ToDictionary(u => u.Id);

        return rounds.Select(r =>
        {
            userMap.TryGetValue(r.UserId, out var u);
            var name = u is null ? "" : $"{u.FirstName} {u.LastName}".Trim();
            var email = u?.Email ?? string.Empty;
            // Match RoundResponse's simple to-par: full/nine-hole tees use full par, otherwise half.
            var par = r.FullRound || r.TeeboxIsNineHole ? r.TeeboxPar : r.TeeboxPar / 2;

            return new AdminRoundListItemDto
            {
                RoundId = r.RoundId,
                UserId = r.UserId,
                PlayerName = string.IsNullOrWhiteSpace(name) ? (string.IsNullOrWhiteSpace(email) ? "Unknown" : email) : name,
                PlayerEmail = email,
                DatePlayed = r.DatePlayed,
                Score = r.Score,
                ToPar = r.Score - par,
                CourseName = r.CourseName,
                TeeboxName = r.TeeboxName,
                ExcludeFromStats = r.ExcludeFromStats,
                UsingShotTracking = r.UsingShotTracking,
                UsingHoleStats = r.UsingHoleStats,
                FullRound = r.FullRound
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
}
