using FairwayFinder.Features.Enums;
using FairwayFinder.Identity;

namespace FairwayFinder.Features.Data;

public class UserProfileResponse
{
    public long UserProfileId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid PublicIdentifier { get; set; }
    public bool IsPublic { get; set; }

    /// <summary>The public stats page is <c>fairwayfinder.pro/golf/{PublicSlug}</c>. Null until first made public.</summary>
    public string? PublicSlug { get; set; }
    public bool ShowStrokesGained { get; set; }
    public bool ShowRecentRounds { get; set; }
    public bool ShowCourseNames { get; set; }
    public string? DisplayName { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public PreferredTees PreferredTees { get; set; }

    /// <summary>The user's default golfer level for strokes-gained calculations.</summary>
    public BaselineLevel SgBaselineLevel { get; set; }
}
