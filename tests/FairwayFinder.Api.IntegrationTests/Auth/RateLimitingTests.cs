using System.Net;
using System.Net.Http.Json;
using FairwayFinder.Api.Auth;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairwayFinder.Api.IntegrationTests.Auth;

/// <summary>
/// Per-IP throttling: the auth endpoints, and paths no endpoint answers (scanners). Each test runs
/// a host of its own with limiting on and tiny limits, so its buckets start empty; the shared host
/// keeps it off (see TestSettings).
/// </summary>
public class RateLimitingTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const int Limit = 3;

    private WebApplicationFactory<Program> LimitedHost() => Factory.WithWebHostBuilder(b => b
        .UseSetting("RateLimiting:Enabled", "true")
        .UseSetting("RateLimiting:Auth:PermitLimit", Limit.ToString())
        .UseSetting("RateLimiting:Auth:WindowSeconds", "60")
        .UseSetting("RateLimiting:Unmatched:PermitLimit", Limit.ToString())
        .UseSetting("RateLimiting:Unmatched:WindowSeconds", "60"));

    /// <summary>A client whose requests appear, through the trusted proxy header, to come from <paramref name="ip"/>.</summary>
    private static HttpClient ClientFrom(WebApplicationFactory<Program> host, string ip)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    private static Task<HttpResponseMessage> AttemptLoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "nobody@test.com", Password = "wrong-password" });

    [Fact]
    public async Task Auth_requests_beyond_the_limit_get_429_with_retry_after()
    {
        using var host = LimitedHost();
        using var client = ClientFrom(host, "203.0.113.10");

        for (var i = 0; i < Limit; i++)
            await AssertStatusAsync(HttpStatusCode.Unauthorized, await AttemptLoginAsync(client));

        var throttled = await AttemptLoginAsync(client);

        await AssertStatusAsync(HttpStatusCode.TooManyRequests, throttled);
        Assert.NotNull(throttled.Headers.RetryAfter);
        Assert.Equal("application/problem+json", throttled.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Auth_limit_is_per_client_ip()
    {
        using var host = LimitedHost();
        using var first = ClientFrom(host, "203.0.113.10");
        using var second = ClientFrom(host, "203.0.113.20");

        for (var i = 0; i < Limit; i++)
            await AttemptLoginAsync(first);
        await AssertStatusAsync(HttpStatusCode.TooManyRequests, await AttemptLoginAsync(first));

        await AssertStatusAsync(HttpStatusCode.Unauthorized, await AttemptLoginAsync(second));
    }

    [Fact]
    public async Task Only_the_proxy_appended_forwarded_entry_counts()
    {
        using var host = LimitedHost();

        // A client rotating a fake left-most entry is still one client: the proxy's entry is last.
        for (var i = 0; i < Limit; i++)
        {
            using var client = ClientFrom(host, $"198.51.100.{i}, 203.0.113.10");
            await AttemptLoginAsync(client);
        }

        using var spoofing = ClientFrom(host, "198.51.100.99, 203.0.113.10");
        await AssertStatusAsync(HttpStatusCode.TooManyRequests, await AttemptLoginAsync(spoofing));
    }

    [Fact]
    public async Task Probing_unknown_paths_is_throttled_without_touching_the_real_api()
    {
        using var host = LimitedHost();
        using var client = ClientFrom(host, "45.148.10.40");

        foreach (var path in new[] { "/login", "/wp-login.php", "/.env" })
            await AssertStatusAsync(HttpStatusCode.NotFound, await client.GetAsync(path));

        await AssertStatusAsync(HttpStatusCode.TooManyRequests, await client.GetAsync("/two-factor-challenge"));

        // The same address still reaches real endpoints: they draw from a separate bucket.
        await AssertStatusAsync(HttpStatusCode.Unauthorized, await AttemptLoginAsync(client));
    }
}
