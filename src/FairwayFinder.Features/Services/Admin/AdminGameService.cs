using System.Text.Json;
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Admin surface over games: inspect any game, and repair the things that make one score wrong —
/// a mistyped handicap, the wrong tees, a mislinked round.
///
/// Follows the same discipline as <see cref="AdminRoundService"/>: it never loosens
/// <see cref="IGameService"/>'s guards, it re-resolves the authoritative identity server-side.
/// Every participant method takes only a participant id — never a game id, never a user id — so a
/// repair cannot move a participant between games or reassign their line to a different golfer,
/// because neither value is an input. <c>adminUserId</c> is used for audit stamping and logging
/// only.
///
/// No <see cref="IPushNotificationService"/> here on purpose: admin repair must not push.
/// </summary>
public class AdminGameService(
    IGameService gameService,
    GameScoreReader reader,
    IGameScoringEngineResolver engineResolver,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<AdminGameService> logger)
{
    /// <summary>Every non-deleted game, newest first, with the host's name for display and filtering.</summary>
    public async Task<List<AdminGameListItemDto>> GetAllGamesAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var games = await db.Games.AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderByDescending(g => g.DatePlayed).ThenByDescending(g => g.GameId)
            .Select(g => new
            {
                g.GameId,
                g.GameType,
                g.State,
                g.HostUserId,
                g.DatePlayed,
                g.JoinCode,
                g.UseNet,
                g.FullRound,
                g.FrontNine,
                g.BackNine,
                CourseName = g.Course.CourseName
            })
            .ToListAsync();

        if (games.Count == 0) return [];

        var gameIds = games.Select(g => g.GameId).ToList();

        // One grouped aggregate for the whole grid rather than a count query per row.
        var counts = await db.GameParticipants.AsNoTracking()
            .Where(p => gameIds.Contains(p.GameId) && !p.IsDeleted)
            .GroupBy(p => p.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countMap = counts.ToDictionary(c => c.GameId, c => c.Count);

        var hostIds = games.Select(g => g.HostUserId).Distinct().ToList();
        var hosts = await db.Users.AsNoTracking()
            .Where(u => hostIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync();

        var hostMap = hosts.ToDictionary(u => u.Id);

        // Display names are resolved in memory — DisplayNameHelper is not EF-translatable.
        return
        [
            .. games.Select(g =>
            {
                hostMap.TryGetValue(g.HostUserId, out var host);
                var email = host?.Email ?? string.Empty;

                return new AdminGameListItemDto
                {
                    GameId = g.GameId,
                    GameType = g.GameType,
                    State = g.State,
                    HostUserId = g.HostUserId,
                    HostName = DisplayNameHelper.BuildForAdmin(host?.FirstName, host?.LastName, email),
                    HostEmail = email,
                    DatePlayed = g.DatePlayed,
                    CourseName = g.CourseName,
                    JoinCode = g.JoinCode,
                    ParticipantCount = countMap.TryGetValue(g.GameId, out var c) ? c : 0,
                    UseNet = g.UseNet,
                    Shape = g.FullRound ? "18" : g.FrontNine ? "Front 9" : g.BackNine ? "Back 9" : "18"
                };
            })
        ];
    }

    /// <summary>
    /// The full game for the detail page. Reads through <see cref="IGameService"/> as a trusted
    /// caller, the same way <c>GetRoundForAdminAsync</c> leans on an ungated round read.
    /// </summary>
    public async Task<AdminGameDetailDto?> GetGameForAdminAsync(long gameId)
    {
        var result = await gameService.GetGameAsync(gameId, userId: null);
        if (!result.IsOk) return null;

        await using var db = await dbContextFactory.CreateDbContextAsync();

        var host = await db.Users.AsNoTracking()
            .Where(u => u.Id == result.Value!.HostUserId)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync();

        var game = await db.Games.AsNoTracking().FirstAsync(g => g.GameId == gameId);

        var participants = await db.GameParticipants.AsNoTracking()
            .Where(p => p.GameId == gameId && !p.IsDeleted)
            .OrderBy(p => p.GameParticipantId)
            .ToListAsync();

        // Read once more for the raw lines. The scorecard grid needs gross, net, and strokes
        // received per hole, none of which survive into the scoreboard the API returns.
        var read = await reader.ReadAsync(game, participants);

        return new AdminGameDetailDto
        {
            State = result.Value!,
            HostName = DisplayNameHelper.BuildForAdmin(host?.FirstName, host?.LastName, host?.Email),
            HostEmail = host?.Email ?? string.Empty,
            FinalScoreboardJson = Prettify(game.FinalScoreboard),
            Lines = read.Context.Participants.ToDictionary(p => p.ParticipantId, p => p.Holes)
        };
    }

    /// <summary>
    /// Teeboxes an admin may move a participant onto: every tee on the game's course, archived
    /// included. A game that really was played off a since-superseded tee has to stay repairable,
    /// so the create-time archived guard does not apply here — the option is badged instead.
    /// </summary>
    public async Task<List<AdminGameTeeboxOptionDto>> GetRepairTeeboxOptionsAsync(long gameId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.GameId == gameId && !g.IsDeleted);
        if (game is null) return [];

        var holeNumbers = GameScoreReader.HoleNumbersFor(game);

        var teeboxes = await db.Teeboxes.AsNoTracking()
            .Where(t => t.CourseId == game.CourseId && !t.IsDeleted)
            .OrderBy(t => t.TeeboxName)
            .Select(t => new { t.TeeboxId, t.TeeboxName, t.ArchivedOn })
            .ToListAsync();

        var teeboxIds = teeboxes.Select(t => t.TeeboxId).ToList();

        var coverage = await db.Holes.AsNoTracking()
            .Where(h => teeboxIds.Contains(h.TeeboxId) && !h.IsDeleted)
            .Select(h => new { h.TeeboxId, h.HoleNumber })
            .ToListAsync();

        var byTeebox = coverage
            .GroupBy(h => h.TeeboxId)
            .ToDictionary(g => g.Key, g => g.Select(h => h.HoleNumber).ToHashSet());

        return
        [
            .. teeboxes.Select(t => new AdminGameTeeboxOptionDto
            {
                TeeboxId = t.TeeboxId,
                TeeboxName = t.TeeboxName,
                IsArchived = t.ArchivedOn is not null,
                CoversGameHoles = byTeebox.TryGetValue(t.TeeboxId, out var holes)
                                  && holeNumbers.All(holes.Contains)
            })
        ];
    }

    /// <summary>
    /// Rounds a participant could legitimately be linked to: their own, on the game's course.
    /// The participant's owner is read from the row, never supplied by the caller.
    /// </summary>
    public async Task<List<AdminGameRoundOptionDto>> GetRelinkCandidatesAsync(long participantId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var participant = await db.GameParticipants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.GameParticipantId == participantId && !p.IsDeleted);

        if (participant?.UserId is null) return [];

        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.GameId == participant.GameId);
        if (game is null) return [];

        return await db.Rounds.AsNoTracking()
            .Where(r => r.UserId == participant.UserId && r.CourseId == game.CourseId && !r.IsDeleted)
            .OrderByDescending(r => r.DatePlayed).ThenByDescending(r => r.RoundId)
            .Select(r => new AdminGameRoundOptionDto
            {
                RoundId = r.RoundId,
                DatePlayed = r.DatePlayed,
                TeeboxName = r.Teebox.TeeboxName,
                Score = r.Score,
                IsComplete = r.IsComplete
            })
            .ToListAsync();
    }

    /// <summary>
    /// Corrects a participant's handicap, tees, or team. Nothing downstream is stored — the
    /// playing handicap, the strokes received, and the whole scoreboard are derived on every read
    /// — so a live game needs no recompute after this.
    /// </summary>
    public async Task<bool> RepairParticipantAsync(
        long participantId, RepairParticipantRequest request, string adminUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var participant = await db.GameParticipants
            .FirstOrDefaultAsync(p => p.GameParticipantId == participantId && !p.IsDeleted);

        if (participant is null)
        {
            logger.LogWarning(
                "Admin {AdminUserId} attempted to repair participant {ParticipantId}, which does not exist.",
                adminUserId, participantId);
            return false;
        }

        if (request.TeeboxId is { } teeboxId && teeboxId != participant.TeeboxId)
        {
            if (participant.RoundId is not null)
            {
                // The linked round's teebox is where its scores' holes live. Changing it here
                // would read par and stroke index off a different hole set than the strokes.
                logger.LogWarning(
                    "Admin {AdminUserId} attempted to change tees on participant {ParticipantId}, which is scored from round {RoundId}.",
                    adminUserId, participantId, participant.RoundId);
                return false;
            }

            var game = await db.Games.AsNoTracking().FirstAsync(g => g.GameId == participant.GameId);
            var valid = await db.Teeboxes.AsNoTracking()
                .AnyAsync(t => t.TeeboxId == teeboxId && t.CourseId == game.CourseId && !t.IsDeleted);

            if (!valid) return false;

            participant.TeeboxId = teeboxId;
        }

        if (request.CourseHandicap is { } handicap) participant.CourseHandicap = handicap;
        if (request.Team is { } team) participant.Team = team;

        participant.UpdatedBy = adminUserId;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Admin {AdminUserId} repaired participant {ParticipantId} in game {GameId}.",
            adminUserId, participantId, participant.GameId);

        return true;
    }

    /// <summary>
    /// Points a participant at a different round of their own, or unlinks them. The round's owner
    /// and course are re-checked against the participant's row, so an admin can fix a mislinked
    /// round but cannot attach a stranger's round to someone else's line.
    /// </summary>
    public async Task<bool> RelinkParticipantRoundAsync(long participantId, long? roundId, string adminUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var participant = await db.GameParticipants
            .FirstOrDefaultAsync(p => p.GameParticipantId == participantId && !p.IsDeleted);

        if (participant is null) return false;

        if (roundId is null)
        {
            participant.RoundId = null;
        }
        else
        {
            var game = await db.Games.AsNoTracking().FirstAsync(g => g.GameId == participant.GameId);

            var round = await db.Rounds.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoundId == roundId.Value && !r.IsDeleted);

            if (round is null || round.UserId != participant.UserId || round.CourseId != game.CourseId)
            {
                logger.LogWarning(
                    "Admin {AdminUserId} attempted to link round {RoundId} to participant {ParticipantId}, which it does not belong to.",
                    adminUserId, roundId, participantId);
                return false;
            }

            participant.RoundId = round.RoundId;

            // Same rule the API path applies: the round's teebox is the source of truth.
            participant.TeeboxId = round.TeeboxId;
        }

        participant.UpdatedBy = adminUserId;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Admin {AdminUserId} relinked participant {ParticipantId} to round {RoundId}.",
            adminUserId, participantId, roundId);

        return true;
    }

    /// <summary>
    /// Re-snapshots a registered golfer's name from their profile. Fixes participants added before
    /// the user filled their name in. Guests are left alone — their name is the only one they have.
    /// </summary>
    public async Task<bool> RefreshParticipantDisplayNameAsync(long participantId, string adminUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var participant = await db.GameParticipants
            .FirstOrDefaultAsync(p => p.GameParticipantId == participantId && !p.IsDeleted);

        if (participant?.UserId is null) return false;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == participant.UserId);
        var name = DisplayNameHelper.Build(user);

        if (string.IsNullOrWhiteSpace(name)) return false;

        participant.DisplayName = name;
        participant.UpdatedBy = adminUserId;

        await db.SaveChangesAsync();

        return true;
    }

    /// <summary>Soft-deletes a participant added by mistake. Unlike the API, allowed on a live game.</summary>
    public async Task<bool> RemoveParticipantAsAdminAsync(long participantId, string adminUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var participant = await db.GameParticipants
            .FirstOrDefaultAsync(p => p.GameParticipantId == participantId && !p.IsDeleted);

        if (participant is null) return false;

        participant.IsDeleted = true;
        participant.UpdatedBy = adminUserId;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Admin {AdminUserId} removed participant {ParticipantId} from game {GameId}.",
            adminUserId, participantId, participant.GameId);

        return true;
    }

    /// <summary>
    /// Re-runs the engine and overwrites the stored snapshot. This is the only repair with stored
    /// state to fix: a live game's scoreboard is derived on every read, so it needs nothing.
    /// Only meaningful once a game is posted.
    /// </summary>
    public async Task<bool> RecomputeFinalScoreboardAsync(long gameId, string adminUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var game = await db.Games.FirstOrDefaultAsync(g => g.GameId == gameId && !g.IsDeleted);
        if (game is null || game.State != GameState.Completed) return false;

        var participants = await db.GameParticipants.AsNoTracking()
            .Where(p => p.GameId == gameId && !p.IsDeleted)
            .OrderBy(p => p.GameParticipantId)
            .ToListAsync();

        var read = await reader.ReadAsync(game, participants);
        var board = engineResolver.For(game.GameType).Score(read.Context);

        // Base type on purpose: the concrete type omits the polymorphic discriminator.
        game.FinalScoreboard = JsonSerializer.Serialize<GameScoreboard>(board);
        game.UpdatedBy = adminUserId;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Admin {AdminUserId} recomputed the final scoreboard for game {GameId}.", adminUserId, gameId);

        return true;
    }

    /// <summary>
    /// Forces a state change — chiefly Completed back to Active so a game can be corrected and
    /// re-posted, or to Abandoned for junk.
    /// </summary>
    public async Task<bool> SetGameStateAsync(long gameId, GameState state, string adminUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var game = await db.Games.FirstOrDefaultAsync(g => g.GameId == gameId && !g.IsDeleted);
        if (game is null) return false;

        var previous = game.State;

        game.State = state;
        game.UpdatedBy = adminUserId;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Admin {AdminUserId} moved game {GameId} from {Previous} to {State}.",
            adminUserId, gameId, previous, state);

        return true;
    }

    public async Task<bool> DeleteGameAsync(long gameId, string adminUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var game = await db.Games.FirstOrDefaultAsync(g => g.GameId == gameId && !g.IsDeleted);
        if (game is null) return false;

        game.IsDeleted = true;
        game.UpdatedBy = adminUserId;

        var participants = await db.GameParticipants
            .Where(p => p.GameId == gameId && !p.IsDeleted)
            .ToListAsync();

        foreach (var participant in participants)
        {
            participant.IsDeleted = true;
            participant.UpdatedBy = adminUserId;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Admin {AdminUserId} deleted game {GameId}.", adminUserId, gameId);

        return true;
    }

    private static string? Prettify(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
