using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// Push notifications fired as a game starts and finishes. Polling stays the source of truth —
/// these are a nudge, nothing more, which is why every one of them is swallowed on failure.
/// Mirrors <see cref="RoundNotifications"/>.
/// </summary>
public static class GameNotifications
{
    /// <summary>
    /// Tells everyone but the host that the game is live. Never throws: a push failure must not
    /// cost the host their game.
    /// </summary>
    public static async Task NotifyParticipantsOfGameStartAsync(
        ApplicationDbContext dbContext,
        IPushNotificationService pushService,
        ILogger logger,
        Game game,
        IReadOnlyList<GameParticipant> participants)
    {
        try
        {
            var courseName = await CourseNameAsync(dbContext, game.CourseId);
            var title = $"{GameLabel(game.GameType)} started";
            var body = $"Your game at {courseName} is underway";

            await NotifyAsync(pushService, participants, game.HostUserId, title, body, GameRoute(game));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify participants that game {GameId} started", game.GameId);
        }
    }

    /// <summary>Tells everyone but the host how the game finished.</summary>
    public static async Task NotifyParticipantsOfGameResultAsync(
        ApplicationDbContext dbContext,
        IPushNotificationService pushService,
        ILogger logger,
        Game game,
        IReadOnlyList<GameParticipant> participants,
        string summary)
    {
        try
        {
            var courseName = await CourseNameAsync(dbContext, game.CourseId);
            var title = $"{GameLabel(game.GameType)} final";

            await NotifyAsync(pushService, participants, game.HostUserId, title, $"{summary} at {courseName}", GameRoute(game));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify participants of the result of game {GameId}", game.GameId);
        }
    }

    /// <summary>
    /// The keys a tap routes on. Values are strings because APNs custom data is JSON and the
    /// client reads them back untyped.
    /// </summary>
    private static Dictionary<string, string> GameRoute(Game game) => new()
    {
        ["type"] = "game",
        ["gameId"] = game.GameId.ToString()
    };

    private static async Task NotifyAsync(
        IPushNotificationService pushService,
        IReadOnlyList<GameParticipant> participants,
        string hostUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data)
    {
        // Guests have no device, and the host already knows — they pressed the button.
        var targets = participants
            .Where(p => p.UserId is not null && p.UserId != hostUserId)
            .Select(p => p.UserId!)
            .Distinct();

        foreach (var userId in targets)
        {
            await pushService.SendToUserAsync(userId, title, body, data: data);
        }
    }

    private static async Task<string> CourseNameAsync(ApplicationDbContext dbContext, long courseId)
    {
        var name = await dbContext.Courses.AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .Select(c => c.CourseName)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(name) ? "the course" : name;
    }

    private static string GameLabel(GameType gameType) => gameType switch
    {
        GameType.MatchPlay => "Match",
        GameType.Skins => "Skins game",
        _ => "Game"
    };
}
