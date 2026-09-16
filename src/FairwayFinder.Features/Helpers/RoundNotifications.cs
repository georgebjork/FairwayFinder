using FairwayFinder.Data;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// Push notifications fired when a round is posted. Shared by the atomic submit path and the
/// incremental path so a golfer's friends see the same message whichever way the round was
/// entered — and so it fires exactly once, when the round is finished.
/// </summary>
public static class RoundNotifications
{
    /// <summary>
    /// Tells the poster's friends they logged a round. Never throws: a push failure must not
    /// cost the golfer their round, so it is logged and swallowed.
    /// </summary>
    public static async Task NotifyFriendsOfNewRoundAsync(
        ApplicationDbContext dbContext,
        IFriendService friendService,
        IPushNotificationService pushService,
        ILogger logger,
        string userId,
        long courseId,
        int score)
    {
        try
        {
            var friends = await friendService.GetFriendsAsync(userId);
            if (friends.Count == 0) return;

            var user = await dbContext.Users.FindAsync(userId);
            var course = await dbContext.Courses.FindAsync(courseId);

            var posterName = !string.IsNullOrWhiteSpace(user?.FirstName) ? user!.FirstName : "A friend";
            var courseName = !string.IsNullOrWhiteSpace(course?.CourseName) ? course!.CourseName : "a course";

            var title = $"{posterName} posted a round";
            var body = $"Shot {score} at {courseName}";

            foreach (var friend in friends)
            {
                await pushService.SendToUserAsync(friend.UserId, title, body);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify friends of new round for user {UserId}", userId);
        }
    }
}
