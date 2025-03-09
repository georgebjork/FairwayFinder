namespace FairwayFinder.Core.Features.Scorecards.Cache;

public static class ScorecardCacheKeys
{
    private const string Scorecard = "ScorecardCache";
    private const string ScorecardStats = "ScorecardStatsCache";
    private const string ScorecardForm = "ScorecardFormCache";

    public static string GetScorecardCacheKey(long roundId) => $"{Scorecard}:RoundId:{roundId}";
    public static string GetScorecardStatsCacheKey(long roundId) => $"{ScorecardStats}:RoundId:{roundId}";
    public static string GetScorecardFormCacheKey(string userId, long roundId) => $"{ScorecardStats}:RoundId:{roundId}UserId:{userId}";
}