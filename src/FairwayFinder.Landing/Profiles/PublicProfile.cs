namespace FairwayFinder.Landing.Profiles;

// The Landing site's own copy of the API's PublicProfileResponse
// (src/FairwayFinder.Features/Data/PublicProfileResponse.cs). Landing references no other project, so
// this is the contract: keep the two in step. Null sections were hidden by the golfer.

public record PublicProfile(
    string DisplayName,
    string Slug,
    DateTime GeneratedOn,
    PublicProfileHeadline Headline,
    PublicStrokesGained? StrokesGained,
    PublicScoringDistribution ScoringDistribution,
    List<PublicTrendPoint> Trend,
    List<PublicRecentRound>? RecentRounds,
    List<PublicScorecard>? Scorecards);

public record PublicProfileHeadline(
    decimal? HandicapIndex,
    double? ScoringAverage18,
    int RoundsThisYear,
    int TotalRounds,
    PublicBestRound? BestRound);

public record PublicBestRound(string? Key, int Score, int ToPar, DateOnly DatePlayed, string? CourseName);

public record PublicStrokesGained(
    double OffTheTee,
    double Approach,
    double AroundTheGreen,
    double Putting,
    double Total,
    int RoundsIncluded);

public record PublicScoringDistribution(
    int EaglesOrBetter,
    int Birdies,
    int Pars,
    int Bogeys,
    int DoubleBogeys,
    int TripleOrWorse,
    int TotalHoles);

public record PublicTrendPoint(DateOnly DatePlayed, int Score, string? CourseName);

public record PublicRecentRound(string? Key, DateOnly DatePlayed, int Score, int ToPar, bool FullRound, string? CourseName);

/// <summary>One round's card, written to golf/{slug}/rounds/{Key}/. Key is the date, never the round id.</summary>
public record PublicScorecard(
    string Key,
    DateOnly DatePlayed,
    string? CourseName,
    string TeeboxName,
    decimal Rating,
    int Slope,
    int Score,
    int ToPar,
    int Par,
    bool FullRound,
    List<PublicScorecardHole> Holes,
    int? FairwaysHit,
    int? FairwaysTotal,
    int? GreensHit,
    int? GreensTotal,
    int? Putts,
    PublicStrokesGained? StrokesGained);

/// <summary>FairwayMiss/GreenMiss: Left, Right, Short or Long; null when hit, untracked or unrecorded.</summary>
public record PublicScorecardHole(
    int HoleNumber,
    int Par,
    int Yardage,
    int Handicap,
    int Score,
    int? Putts,
    bool? Fairway,
    bool? Green,
    string? FairwayMiss,
    string? GreenMiss);
