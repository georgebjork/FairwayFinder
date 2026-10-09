using Microsoft.AspNetCore.Hosting;

namespace FairwayFinder.IntegrationTests.Common;

/// <summary>
/// Every configuration key either host refuses to start without, plus the URL bases the invite
/// and password-reset links are built from so tests can assert on them.
/// </summary>
/// <remarks>
/// Applied with <see cref="IWebHostBuilder.UseSetting"/> rather than ConfigureAppConfiguration:
/// both Program.cs files read the connection string, Jwt and Apns sections before
/// <c>builder.Build()</c>, and only host settings are visible that early under minimal hosting.
/// </remarks>
public static class TestSettings
{
    public const string JwtIssuer = "FairwayFinder.Api";
    public const string JwtAudience = "FairwayFinder.Mobile";
    public const string JwtSecret = "integration-tests-signing-key-that-is-long-enough-0123456789";

    public const string RegistrationUrlBase = "https://test.fairwayfinder.pro/register";
    public const string PasswordResetUrlBase = "https://test.fairwayfinder.pro/reset-password";
    public const string AppInstallUrl = "https://test.fairwayfinder.pro/install";

    public const string TgtrBaseUrl = "https://tgtr.test/";
    public const string GolfCourseApiBaseUrl = "https://golfcourseapi.test/";

    public static Dictionary<string, string?> For(string connectionString) => new()
    {
        ["ConnectionStrings:fairwayfinder"] = connectionString,

        ["Jwt:Secret"] = JwtSecret,
        ["Jwt:Issuer"] = JwtIssuer,
        ["Jwt:Audience"] = JwtAudience,
        ["Jwt:AccessTokenExpirationMinutes"] = "15",
        ["Jwt:RefreshTokenExpirationDays"] = "30",

        // Never used to reach Apple: FakeApnsClient replaces the real client.
        ["Apns:BundleId"] = "test.bundle",
        ["Apns:KeyId"] = "TESTKEYID",
        ["Apns:TeamId"] = "TESTTEAMID",
        ["Apns:P8Contents"] = "not-a-real-key",
        ["Apns:UseSandbox"] = "true",

        ["Invites:RegistrationUrlBase"] = RegistrationUrlBase,
        ["Invites:AppInstallUrl"] = AppInstallUrl,
        ["Auth:PasswordResetUrlBase"] = PasswordResetUrlBase,

        ["Tgtr:BaseUrl"] = TgtrBaseUrl,
        ["GolfCourseApi:BaseUrl"] = GolfCourseApiBaseUrl,
        ["GolfCourseApi:ApiKey"] = "test-key",

        ["Resend:ApiKey"] = "re_test",
        ["RequestLogging:Enabled"] = "false",

        ["SeedUser:Email"] = "",
        ["SeedUser:Password"] = "",
    };

    public static IWebHostBuilder UseTestSettings(
        this IWebHostBuilder builder,
        string connectionString,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var settings = For(connectionString);
        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
                settings[key] = value;
        }

        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        return builder;
    }
}
