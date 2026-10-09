using FairwayFinder.Features.HttpClients;
using FairwayFinder.IntegrationTests.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Admin.IntegrationTests.Infrastructure;

/// <summary>
/// The real admin console host over a throwaway Postgres. Unlike the API, the admin host owns
/// migrations, roles and the seed user, so its own startup path builds the schema here.
/// </summary>
public sealed class AdminFactory : FairwayFinderFactory<Program>
{
    public const string SeedAdminEmail = "seed-admin@test.fairwayfinder.pro";
    public const string SeedAdminPassword = "seed-admin-password";

    protected override IReadOnlyDictionary<string, string?> SettingOverrides { get; } = new Dictionary<string, string?>
    {
        ["SeedUser:Email"] = SeedAdminEmail,
        ["SeedUser:Password"] = SeedAdminPassword,
        ["SeedUser:FirstName"] = "Seed",
        ["SeedUser:LastName"] = "Admin",
    };

    /// <summary>
    /// Touching <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.Services"/>
    /// boots the host, and Program.cs migrates and seeds before it starts listening.
    /// </summary>
    protected override Task PrepareDatabaseAsync()
    {
        _ = Services;
        return Task.CompletedTask;
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Authenticate from the test headers when present, otherwise fall back to the real Identity
        // cookie. Challenge and forbid stay on the cookie scheme, so the /login and /access-denied
        // redirects are the production ones.
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })
            .AddPolicyScheme("TestOrCookie", "Test header or Identity cookie", options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(TestAuthHandler.UserHeader)
                        ? TestAuthHandler.SchemeName
                        : IdentityConstants.ApplicationScheme;
            });

        services.PostConfigure<AuthenticationOptions>(options =>
            options.DefaultAuthenticateScheme = "TestOrCookie");

        // Third-party APIs answer from canned responses. The handler is shared and long-lived, so
        // the client factory must never rotate (and dispose) it.
        services.AddHttpClient<GolfCourseApiHttpClient>()
            .ConfigurePrimaryHttpMessageHandler(() => ExternalHttp)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        services.AddHttpClient<TgtrHttpClient>()
            .ConfigurePrimaryHttpMessageHandler(() => ExternalHttp)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
    }
}

[CollectionDefinition(Name)]
public sealed class AdminCollection : ICollectionFixture<AdminFactory>
{
    public const string Name = "Admin";
}
