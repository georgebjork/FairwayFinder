using FairwayFinder.Identity;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// How a golfer's name is rendered, in one place. This logic used to be copy-pasted at six call
/// sites with three signatures and three different empty-cases, which is why the overloads below
/// differ rather than collapsing into one method — each preserves the contract its callers rely on.
///
/// None of it is EF-translatable. Call it after <c>ToListAsync()</c>, the way
/// <c>AdminRoundService.GetAllRoundsAsync</c> does.
/// </summary>
public static class DisplayNameHelper
{
    /// <summary>
    /// First + last when either is set, else the user name, else empty. The API-facing contract.
    /// </summary>
    public static string Build(string? firstName, string? lastName, string? userName)
    {
        if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
        {
            return $"{firstName} {lastName}".Trim();
        }

        return userName ?? string.Empty;
    }

    /// <summary>
    /// Null for a null user, so an absent profile stays distinguishable from a nameless one.
    /// </summary>
    public static string? Build(ApplicationUser? user)
        => user is null ? null : Build(user.FirstName, user.LastName, user.UserName);

    /// <summary>
    /// Name, else email, else "Unknown". The admin-grid contract — a row always has something to
    /// show, and an email is more useful to an admin than a blank cell.
    /// </summary>
    public static string BuildForAdmin(string? firstName, string? lastName, string? email)
    {
        var name = Build(firstName, lastName, userName: null);
        if (!string.IsNullOrWhiteSpace(name)) return name;

        return string.IsNullOrWhiteSpace(email) ? "Unknown" : email;
    }
}
