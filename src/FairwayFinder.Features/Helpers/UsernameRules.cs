using System.Text;
using System.Text.RegularExpressions;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// What a username may look like, in one place. Pure, so the API validator, the service and the
/// tests all agree. Usernames are stored lowercase, which is what makes the plain unique index on
/// <c>AspNetUsers."Handle"</c> case-insensitive.
///
/// The <c>AddUserHandle</c> migration backfilled existing accounts with a SQL copy of
/// <see cref="BuildBase"/> and <see cref="Reserved"/>; keep them in step if either changes.
/// </summary>
public static partial class UsernameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 20;

    /// <summary>The longest generated base, leaving room for a 4-digit disambiguating suffix.</summary>
    public const int MaxBaseLength = 15;

    public const string FallbackBase = "golfer";

    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "admin", "administrator", "api", "fairwayfinder", "fairway_finder", "help", "me", "moderator",
        "null", "official", "root", "staff", "support", "system", "undefined"
    };

    // 3–20 chars of a-z 0-9 _ . — starts with a letter or digit, no "..", no trailing period.
    [GeneratedRegex(@"^(?=.{3,20}$)[a-z0-9](?:[a-z0-9_]|\.(?!\.))*[a-z0-9_]$")]
    private static partial Regex Pattern();

    /// <summary>Trims, drops a leading "@" (people type it), and lowercases.</summary>
    public static string Normalize(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.StartsWith('@')) s = s[1..];
        return s.ToLowerInvariant();
    }

    /// <summary>Null when <paramref name="normalized"/> is acceptable, else a message for the golfer.</summary>
    public static string? Validate(string normalized)
    {
        if (normalized.Length < MinLength || normalized.Length > MaxLength)
            return $"Usernames must be {MinLength} to {MaxLength} characters.";

        if (!Pattern().IsMatch(normalized))
            return "Usernames may only use letters, numbers, underscores and periods, must start with a letter or number, and can't end with a period or use two periods in a row.";

        if (Reserved.Contains(normalized))
            return "That username is reserved.";

        return null;
    }

    /// <summary>
    /// The default username stem: first + last name with accents folded and everything but a-z 0-9
    /// dropped, cut to <see cref="MaxBaseLength"/>; "golfer" when that leaves fewer than 3 chars.
    /// </summary>
    public static string BuildBase(string? firstName, string? lastName)
    {
        var decomposed = $"{firstName}{lastName}".ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(MaxBaseLength);
        foreach (var c in decomposed)
        {
            if (sb.Length == MaxBaseLength) break;
            // FormD splits "é" into "e" + a combining accent; keeping only a-z 0-9 drops the accent
            // along with spaces, hyphens, apostrophes and anything non-Latin.
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
        }

        return sb.Length < MinLength ? FallbackBase : sb.ToString();
    }
}
