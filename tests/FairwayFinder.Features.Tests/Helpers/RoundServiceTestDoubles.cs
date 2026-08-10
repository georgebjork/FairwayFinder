using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Features.Tests.Helpers;

/// <summary>
/// Stand-in for <see cref="IFriendService"/> so <c>RoundService</c> can be constructed in tests.
/// Every member throws: the paths under test never reach friend lookups, and a throw makes it
/// obvious if that ever changes rather than silently returning empty data.
/// </summary>
public sealed class ThrowingFriendService : IFriendService
{
    public Task<List<UserSearchResultResponse>> SearchUsersAsync(string viewerUserId, string query, int take = 20)
        => throw new NotSupportedException();

    public Task<List<FriendResponse>> GetFriendsAsync(string userId) => throw new NotSupportedException();

    public Task<List<FriendRequestResponse>> GetIncomingRequestsAsync(string userId) => throw new NotSupportedException();

    public Task<List<FriendRequestResponse>> GetOutgoingRequestsAsync(string userId) => throw new NotSupportedException();

    public Task<int> GetIncomingRequestCountAsync(string userId) => throw new NotSupportedException();

    public Task<long> SendRequestAsync(string requesterUserId, string addresseeUserId) => throw new NotSupportedException();

    public Task<bool> AcceptRequestAsync(long friendshipId, string userId) => throw new NotSupportedException();

    public Task<bool> RejectRequestAsync(long friendshipId, string userId) => throw new NotSupportedException();

    public Task<bool> CancelRequestAsync(long friendshipId, string userId) => throw new NotSupportedException();

    public Task<bool> RemoveFriendAsync(long friendshipId, string userId) => throw new NotSupportedException();

    public Task<FriendshipStatusInfo> GetFriendshipStatusWithUserAsync(string viewerUserId, string targetUserId)
        => throw new NotSupportedException();

    public Task<bool> AreFriendsAsync(string userIdA, string userIdB) => throw new NotSupportedException();
}

/// <summary>
/// Stand-in for <see cref="IPushNotificationService"/>. Sends are swallowed rather than thrown —
/// round writes fire notifications as a side effect, and that isn't what these tests assert on.
/// </summary>
public sealed class NoOpPushNotificationService : IPushNotificationService
{
    public Task RegisterDeviceAsync(string userId, string deviceToken, string? deviceName, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task UnregisterDeviceAsync(string deviceToken, CancellationToken ct = default) => Task.CompletedTask;

    public Task<int> SendToUserAsync(string userId, string title, string body, int? badge = null, CancellationToken ct = default)
        => Task.FromResult(0);
}
