using System.Net.Http.Json;
using System.Text.Json;

namespace FairwayFinder.Landing.Profiles;

/// <summary>
/// Fetches public profiles from the API at build time. Configured by the <c>PublicProfiles</c> section
/// of appsettings (<c>ApiUrl</c>, and <c>Slugs</c> as a comma-separated list). The environment
/// variables <c>PUBLIC_PROFILE_API_URL</c> and <c>PUBLIC_PROFILE_SLUGS</c> override it when non-blank:
/// App Platform sets them as build-time variables, and the AppHost sets the API URL.
/// </summary>
public sealed class ProfileSource(HttpClient http, IReadOnlyList<string> slugs)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<string> Slugs => slugs;

    /// <summary>Null when the API URL or the slug list is blank: the site builds without profile pages.</summary>
    public static ProfileSource? FromConfiguration(IConfiguration configuration)
    {
        var apiUrl = Setting(configuration, "PUBLIC_PROFILE_API_URL", "PublicProfiles:ApiUrl");
        var slugs = (Setting(configuration, "PUBLIC_PROFILE_SLUGS", "PublicProfiles:Slugs") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (string.IsNullOrWhiteSpace(apiUrl) || slugs.Count == 0)
            return null;

        var http = new HttpClient
        {
            BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FairwayFinder-Landing-Build/1.0");

        return new ProfileSource(http, slugs);
    }

    // A blank variable falls through to appsettings, so the AppHost passing an empty value doesn't
    // wipe out what's in appsettings.Development.json.
    private static string? Setting(IConfiguration configuration, string variable, string key)
    {
        var value = configuration[variable];
        return string.IsNullOrWhiteSpace(value) ? configuration[key] : value;
    }

    /// <summary>
    /// Throws on anything but a 200, so a down API or a profile turned private fails the build and
    /// App Platform keeps serving the previous deploy rather than publishing a broken page.
    /// </summary>
    public async Task<PublicProfile> FetchAsync(string slug)
    {
        using var response = await http.GetAsync($"api/public/profiles/{Uri.EscapeDataString(slug)}");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Public profile '{slug}' returned {(int)response.StatusCode} {response.ReasonPhrase}. " +
                "Is the profile public, and is the slug in PUBLIC_PROFILE_SLUGS right?");

        return await response.Content.ReadFromJsonAsync<PublicProfile>(Json)
               ?? throw new InvalidOperationException($"Public profile '{slug}' returned an empty body.");
    }
}
