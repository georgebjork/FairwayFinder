using System.Text.Json;
using FairwayFinder.Core.Authorization.CacheKeys;
using FairwayFinder.Core.Authorization.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Authorization;

public class ScorecardAuthorizationService
{
    private readonly ILogger<ScorecardAuthorizationService> _logger;
    private readonly IAuthorizationRepository _repository;
    private readonly IDistributedCache _cache;

    public ScorecardAuthorizationService(IDistributedCache cache, ILogger<ScorecardAuthorizationService> logger, IAuthorizationRepository repository)
    {
        _cache = cache;
        _logger = logger;
        _repository = repository;
    }
    
    public async Task<bool> CanEditScorecard(long roundId, string userId)
    {
        // Get the cached scorecard IDs from Redis
        var scorecardCacheKey = AuthorizationCacheKey.GetScorecardCacheKey(userId);
        var scorecardCache = await _cache.GetStringAsync(scorecardCacheKey);

        if (!string.IsNullOrEmpty(scorecardCache))
        {
            var cachedScorecardIds = JsonSerializer.Deserialize<List<long>>(scorecardCache);
            if (cachedScorecardIds != null && cachedScorecardIds.Contains(roundId))
            {
                return true;
            }
        }

        // If not in cache, fetch the updated list from repository
        var refreshedIds = await _repository.GetScorecardsByUserId(userId);

        // Update the cache with the refreshed scorecard IDs
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) // Cache expiration time
        };

        await _cache.SetStringAsync(scorecardCacheKey, JsonSerializer.Serialize(refreshedIds), cacheOptions);

        // Check if the refreshed list contains the roundId
        return refreshedIds.Contains(roundId);
    }
}