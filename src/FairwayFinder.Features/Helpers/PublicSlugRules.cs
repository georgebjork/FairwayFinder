using System.Text.RegularExpressions;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// What a public profile slug may look like (<c>fairwayfinder.pro/golf/{slug}</c>). Pure, so the
/// service, the API and the tests agree. Slugs are stored lowercase, which is what makes the unique
/// index on <c>user_profile.public_slug</c> case-insensitive.
/// </summary>
public static partial class PublicSlugRules
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    /// <summary>
    /// The username reserved list, plus names that would collide with files the Landing site writes
    /// beside each profile page.
    /// </summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(
        UsernameRules.Reserved.Concat(["badge", "card", "golf", "index"]), StringComparer.Ordinal);

    // 3–30 chars of a-z 0-9 and single hyphens; starts and ends with a letter or digit.
    [GeneratedRegex(@"^(?=.{3,30}$)[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Pattern();

    /// <summary>Trims and lowercases.</summary>
    public static string Normalize(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Null when <paramref name="normalized"/> is acceptable, else a message for the golfer.</summary>
    public static string? Validate(string normalized)
    {
        if (normalized.Length < MinLength || normalized.Length > MaxLength)
            return $"Profile links must be {MinLength} to {MaxLength} characters.";

        if (!Pattern().IsMatch(normalized))
            return "Profile links may only use letters, numbers and single hyphens, and must start and end with a letter or number.";

        if (Reserved.Contains(normalized))
            return "That profile link is reserved.";

        return null;
    }

    /// <summary>
    /// The slug suggested the first time a profile goes public: the username with underscores and
    /// periods turned into hyphens. May still need validating (e.g. a 3-char handle like "a_b").
    /// </summary>
    public static string FromHandle(string handle)
    {
        var s = Regex.Replace(Normalize(handle), "[^a-z0-9]+", "-").Trim('-');
        return s.Length > MaxLength ? s[..MaxLength].TrimEnd('-') : s;
    }
}
