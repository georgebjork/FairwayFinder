using System.Net;
using FairwayFinder.Identity;
using FairwayFinder.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Admin.IntegrationTests.Infrastructure;

/// <summary>
/// Base for admin console tests: one shared host and database (so the classes run serially in
/// <see cref="AdminCollection"/>), reset before each test.
/// </summary>
[Collection(AdminCollection.Name)]
public abstract class AdminTestBase(AdminFactory factory) : IntegrationTestBase<AdminFactory, Program>(factory)
{
    /// <summary>
    /// A browser-like client: https (the auth cookie is Secure-only), cookies kept, redirects not
    /// followed so tests can assert on where they point.
    /// </summary>
    protected HttpClient BrowserClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost"),
    });

    /// <summary>A client signed in through the test auth scheme as the given user and roles.</summary>
    protected HttpClient ClientAs(string userId, params string[] roles)
    {
        var client = BrowserClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    /// <summary>Creates a real admin account and a client signed in as them.</summary>
    protected async Task<(ApplicationUser Admin, HttpClient Client)> SignInAdminAsync()
    {
        var admin = await Data.CreateAdminAsync();
        return (admin, ClientAs(admin.Id, ApplicationRoles.Admin));
    }

    /// <summary>Resolves an admin service the way a Blazor circuit would: from a fresh scope.</summary>
    protected T Service<T>() where T : notnull =>
        Services.CreateScope().ServiceProvider.GetRequiredService<T>();

    protected static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected} from {response.RequestMessage?.Method} " +
                        $"{response.RequestMessage?.RequestUri?.PathAndQuery} but got " +
                        $"{(int)response.StatusCode} {response.StatusCode} " +
                        $"(Location: {response.Headers.Location}).\n{Truncate(body)}");
        }
    }

    private static string Truncate(string body) => body.Length > 4000 ? body[..4000] + "…" : body;
}
