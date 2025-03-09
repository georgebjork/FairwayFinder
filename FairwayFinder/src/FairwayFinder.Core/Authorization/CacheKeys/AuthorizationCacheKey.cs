namespace FairwayFinder.Core.Authorization.CacheKeys;

public static class AuthorizationCacheKey
{
    public const string ScorecardCacheKey = "ScorecardCacheKey";

    public static string GetScorecardCacheKey(string userId) => $"{ScorecardCacheKey}-UserId:{userId}";
}