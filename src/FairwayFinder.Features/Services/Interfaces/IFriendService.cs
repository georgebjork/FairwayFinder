using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface IFriendService
{
    Task<List<UserSearchResultResponse>> SearchUsersAsync(string viewerUserId, string query, int take = 20);

    Task<List<FriendResponse>> GetFriendsAsync(string userId);

    Task<List<FriendRequestResponse>> GetIncomingRequestsAsync(string userId);

    Task<List<FriendRequestResponse>> GetOutgoingRequestsAsync(string userId);

    Task<int> GetIncomingRequestCountAsync(string userId);

    Task<long> SendRequestAsync(string requesterUserId, string addresseeUserId);

    Task<bool> AcceptRequestAsync(long friendshipId, string userId);

    Task<bool> RejectRequestAsync(long friendshipId, string userId);

    Task<bool> CancelRequestAsync(long friendshipId, string userId);

    Task<bool> RemoveFriendAsync(long friendshipId, string userId);

    Task<FriendshipStatusInfo> GetFriendshipStatusWithUserAsync(string viewerUserId, string targetUserId);

    Task<bool> AreFriendsAsync(string userIdA, string userIdB);
}
