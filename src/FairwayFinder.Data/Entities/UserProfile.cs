using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

public class UserProfile : IAuditable
{
    public long UserProfileId { get; set; }

    public string UserId { get; set; } = null!;

    public Guid PublicIdentifier { get; set; }

    /// <summary>
    /// Master switch for the public stats page at <c>fairwayfinder.pro/golf/{PublicSlug}</c>.
    /// Off by default: nothing about a golfer is public until they opt in.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// The public page's URL segment. Separate from the username so renaming yourself doesn't break
    /// a link already printed on a resume. Null until the profile is first made public.
    /// </summary>
    public string? PublicSlug { get; set; }

    /// <summary>Show the strokes-gained chart on the public page.</summary>
    public bool ShowStrokesGained { get; set; } = true;

    /// <summary>Show the recent-rounds table on the public page.</summary>
    public bool ShowRecentRounds { get; set; } = true;

    /// <summary>Name the courses on the public page. Off hides every course name.</summary>
    public bool ShowCourseNames { get; set; } = true;

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
}
