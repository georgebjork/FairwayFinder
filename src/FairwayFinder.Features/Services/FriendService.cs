using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class FriendService : IFriendService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;

    public FriendService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        UserManager<ApplicationUser> userManager)
    {
        _dbContextFactory = dbContextFactory;
        _userManager = userManager;
    }

    public async Task<List<UserSearchResultResponse>> SearchUsersAsync(string viewerUserId, string query, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return new List<UserSearchResultResponse>();
        }

        var pattern = $"%{query.Trim()}%";

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Match against FirstName, LastName, or "FirstName LastName" (case-insensitive ILIKE).
        // Inner-join UserProfile so users without a profile aren't returned. Exclude self.
        var matchedUsers = await (
            from user in dbContext.Users.AsNoTracking()
            join profile in dbContext.UserProfiles.AsNoTracking()
                on user.Id equals profile.UserId
            where profile.IsDeleted == false
                  && user.Id != viewerUserId
                  && (
                      EF.Functions.ILike(user.FirstName ?? string.Empty, pattern)
                      || EF.Functions.ILike(user.LastName ?? string.Empty, pattern)
                      || EF.Functions.ILike(((user.FirstName ?? string.Empty) + " " + (user.LastName ?? string.Empty)).Trim(), pattern)
                  )
            select new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.UserName,
                user.Email,
                profile.PublicIdentifier
            }
        ).Take(take).ToListAsync();

        if (matchedUsers.Count == 0)
        {
            return new List<UserSearchResultResponse>();
        }

        var matchedIds = matchedUsers.Select(u => u.Id).ToList();

        var friendships = await dbContext.Friendships.AsNoTracking()
            .Where(f => !f.IsDeleted
                        && (
                            (f.RequesterUserId == viewerUserId && matchedIds.Contains(f.AddresseeUserId))
                            || (f.AddresseeUserId == viewerUserId && matchedIds.Contains(f.RequesterUserId))
                        )
                        && (f.Status == FriendshipStatus.Pending || f.Status == FriendshipStatus.Accepted))
            .ToListAsync();

        var results = matchedUsers.Select(u =>
        {
            var f = friendships.FirstOrDefault(x =>
                (x.RequesterUserId == viewerUserId && x.AddresseeUserId == u.Id)
                || (x.AddresseeUserId == viewerUserId && x.RequesterUserId == u.Id));

            FriendshipState state;
            long? friendshipId = null;
            if (f is null)
            {
                state = FriendshipState.None;
            }
            else
            {
                friendshipId = f.FriendshipId;
                if (f.Status == FriendshipStatus.Accepted)
                    state = FriendshipState.Accepted;
                else if (f.RequesterUserId == viewerUserId)
                    state = FriendshipState.PendingOutgoing;
                else
                    state = FriendshipState.PendingIncoming;
            }

            return new UserSearchResultResponse
            {
                UserId = u.Id,
                PublicIdentifier = u.PublicIdentifier,
                DisplayName = BuildDisplayName(u.FirstName, u.LastName, u.UserName),
                Email = u.Email ?? string.Empty,
                FriendshipState = state,
                FriendshipId = friendshipId
            };
        }).ToList();

        return results;
    }

    public async Task<List<FriendResponse>> GetFriendsAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var rows = await (
            from f in dbContext.Friendships.AsNoTracking()
            where !f.IsDeleted && f.Status == FriendshipStatus.Accepted
                  && (f.RequesterUserId == userId || f.AddresseeUserId == userId)
            let otherUserId = f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId
            join user in dbContext.Users.AsNoTracking() on otherUserId equals user.Id
            join profile in dbContext.UserProfiles.AsNoTracking() on otherUserId equals profile.UserId
            where !profile.IsDeleted
            orderby f.CreatedOn descending
            select new
            {
                f.FriendshipId,
                OtherUserId = otherUserId,
                profile.PublicIdentifier,
                profile.IsPublic,
                FriendsSince = f.CreatedOn,
                user.FirstName,
                user.LastName,
                user.UserName,
                user.Email
            }
        ).ToListAsync();

        return rows.Select(r => new FriendResponse
        {
            FriendshipId = r.FriendshipId,
            UserId = r.OtherUserId,
            PublicIdentifier = r.PublicIdentifier,
            DisplayName = BuildDisplayName(r.FirstName, r.LastName, r.UserName),
            Email = r.Email ?? string.Empty,
            IsPublic = r.IsPublic,
            FriendsSince = r.FriendsSince
        }).ToList();
    }

    public async Task<List<FriendRequestResponse>> GetIncomingRequestsAsync(string userId)
    {
        return await GetPendingRequestsAsync(userId, FriendRequestDirection.Incoming);
    }

    public async Task<List<FriendRequestResponse>> GetOutgoingRequestsAsync(string userId)
    {
        return await GetPendingRequestsAsync(userId, FriendRequestDirection.Outgoing);
    }

    public async Task<int> GetIncomingRequestCountAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Friendships.AsNoTracking()
            .CountAsync(f => !f.IsDeleted
                             && f.Status == FriendshipStatus.Pending
                             && f.AddresseeUserId == userId);
    }

    public async Task<long> SendRequestAsync(string requesterUserId, string addresseeUserId)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId) || string.IsNullOrWhiteSpace(addresseeUserId))
        {
            throw new ArgumentException("UserIds must be supplied.");
        }

        if (requesterUserId == addresseeUserId)
        {
            throw new InvalidOperationException("Cannot send a friend request to yourself.");
        }

        var addressee = await _userManager.FindByIdAsync(addresseeUserId);
        if (addressee is null)
        {
            throw new InvalidOperationException("Addressee user not found.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var existing = await dbContext.Friendships
            .FirstOrDefaultAsync(f => !f.IsDeleted
                                      && (
                                          (f.RequesterUserId == requesterUserId && f.AddresseeUserId == addresseeUserId)
                                          || (f.RequesterUserId == addresseeUserId && f.AddresseeUserId == requesterUserId)
                                      ));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nowUtc = DateTime.UtcNow;

        if (existing is not null)
        {
            switch (existing.Status)
            {
                case FriendshipStatus.Accepted:
                    throw new InvalidOperationException("You are already friends with this user.");

                case FriendshipStatus.Pending:
                    if (existing.RequesterUserId == addresseeUserId && existing.AddresseeUserId == requesterUserId)
                    {
                        // The other user already sent a request — auto-accept in place.
                        existing.Status = FriendshipStatus.Accepted;
                        existing.RespondedOn = nowUtc;
                        existing.UpdatedBy = requesterUserId;
                        existing.UpdatedOn = today;
                        await dbContext.SaveChangesAsync();
                        return existing.FriendshipId;
                    }
                    throw new InvalidOperationException("A friend request is already pending.");

                case FriendshipStatus.Rejected:
                case FriendshipStatus.Cancelled:
                    // Reactivate in place: keep the same FriendshipId, swap direction to current sender.
                    existing.RequesterUserId = requesterUserId;
                    existing.AddresseeUserId = addresseeUserId;
                    existing.Status = FriendshipStatus.Pending;
                    existing.RespondedOn = null;
                    existing.UpdatedBy = requesterUserId;
                    existing.UpdatedOn = today;
                    await dbContext.SaveChangesAsync();
                    return existing.FriendshipId;
            }
        }

        var friendship = new Friendship
        {
            RequesterUserId = requesterUserId,
            AddresseeUserId = addresseeUserId,
            Status = FriendshipStatus.Pending,
            RespondedOn = null,
            CreatedBy = requesterUserId,
            CreatedOn = today,
            UpdatedBy = requesterUserId,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.Friendships.Add(friendship);
        await dbContext.SaveChangesAsync();
        return friendship.FriendshipId;
    }

    public async Task<bool> AcceptRequestAsync(long friendshipId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var friendship = await dbContext.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId && !f.IsDeleted);

        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.AddresseeUserId != userId)
        {
            return false;
        }

        friendship.Status = FriendshipStatus.Accepted;
        friendship.RespondedOn = DateTime.UtcNow;
        friendship.UpdatedBy = userId;
        friendship.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectRequestAsync(long friendshipId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var friendship = await dbContext.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId && !f.IsDeleted);

        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.AddresseeUserId != userId)
        {
            return false;
        }

        friendship.Status = FriendshipStatus.Rejected;
        friendship.RespondedOn = DateTime.UtcNow;
        friendship.UpdatedBy = userId;
        friendship.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelRequestAsync(long friendshipId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var friendship = await dbContext.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId && !f.IsDeleted);

        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.RequesterUserId != userId)
        {
            return false;
        }

        friendship.Status = FriendshipStatus.Cancelled;
        friendship.RespondedOn = DateTime.UtcNow;
        friendship.UpdatedBy = userId;
        friendship.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFriendAsync(long friendshipId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var friendship = await dbContext.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId && !f.IsDeleted);

        if (friendship is null
            || friendship.Status != FriendshipStatus.Accepted
            || (friendship.RequesterUserId != userId && friendship.AddresseeUserId != userId))
        {
            return false;
        }

        friendship.IsDeleted = true;
        friendship.UpdatedBy = userId;
        friendship.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<FriendshipStatusInfo> GetFriendshipStatusWithUserAsync(string viewerUserId, string targetUserId)
    {
        if (viewerUserId == targetUserId)
        {
            return new FriendshipStatusInfo { State = FriendshipState.None };
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var f = await dbContext.Friendships.AsNoTracking()
            .Where(f => !f.IsDeleted
                        && (f.Status == FriendshipStatus.Pending || f.Status == FriendshipStatus.Accepted)
                        && (
                            (f.RequesterUserId == viewerUserId && f.AddresseeUserId == targetUserId)
                            || (f.AddresseeUserId == viewerUserId && f.RequesterUserId == targetUserId)
                        ))
            .FirstOrDefaultAsync();

        if (f is null)
        {
            return new FriendshipStatusInfo { State = FriendshipState.None };
        }

        FriendshipState state;
        if (f.Status == FriendshipStatus.Accepted)
            state = FriendshipState.Accepted;
        else if (f.RequesterUserId == viewerUserId)
            state = FriendshipState.PendingOutgoing;
        else
            state = FriendshipState.PendingIncoming;

        return new FriendshipStatusInfo { State = state, FriendshipId = f.FriendshipId };
    }

    public async Task<bool> AreFriendsAsync(string userIdA, string userIdB)
    {
        if (string.IsNullOrWhiteSpace(userIdA) || string.IsNullOrWhiteSpace(userIdB) || userIdA == userIdB)
        {
            return false;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Friendships.AsNoTracking()
            .AnyAsync(f => !f.IsDeleted
                           && f.Status == FriendshipStatus.Accepted
                           && (
                               (f.RequesterUserId == userIdA && f.AddresseeUserId == userIdB)
                               || (f.RequesterUserId == userIdB && f.AddresseeUserId == userIdA)
                           ));
    }

    private async Task<List<FriendRequestResponse>> GetPendingRequestsAsync(string userId, FriendRequestDirection direction)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var rows = await (
            from f in dbContext.Friendships.AsNoTracking()
            where !f.IsDeleted && f.Status == FriendshipStatus.Pending
                  && (direction == FriendRequestDirection.Incoming
                      ? f.AddresseeUserId == userId
                      : f.RequesterUserId == userId)
            let otherUserId = direction == FriendRequestDirection.Incoming ? f.RequesterUserId : f.AddresseeUserId
            join user in dbContext.Users.AsNoTracking() on otherUserId equals user.Id
            join profile in dbContext.UserProfiles.AsNoTracking() on otherUserId equals profile.UserId
            where !profile.IsDeleted
            orderby f.CreatedOn descending
            select new
            {
                f.FriendshipId,
                OtherUserId = otherUserId,
                profile.PublicIdentifier,
                user.FirstName,
                user.LastName,
                user.UserName,
                user.Email,
                f.CreatedOn
            }
        ).ToListAsync();

        return rows.Select(r => new FriendRequestResponse
        {
            FriendshipId = r.FriendshipId,
            OtherUserId = r.OtherUserId,
            OtherPublicIdentifier = r.PublicIdentifier,
            OtherDisplayName = BuildDisplayName(r.FirstName, r.LastName, r.UserName),
            OtherEmail = r.Email ?? string.Empty,
            Direction = direction,
            RequestedOn = r.CreatedOn
        }).ToList();
    }

    private static string BuildDisplayName(string? firstName, string? lastName, string? userName)
    {
        if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
        {
            return $"{firstName} {lastName}".Trim();
        }
        return userName ?? string.Empty;
    }
}
