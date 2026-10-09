using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FairwayFinder.Api.Auth;
using FairwayFinder.Features.Data;
using FairwayFinder.Identity;
using FairwayFinder.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairwayFinder.Api.IntegrationTests.Infrastructure;

/// <summary>A signed-in golfer: their account and an <see cref="HttpClient"/> carrying their access token.</summary>
public sealed record ApiUser(ApplicationUser User, HttpClient Client, LoginResponse Login)
{
    public string Id => User.Id;
    public string Email => User.Email!;
}

/// <summary>
/// Base for API tests. All API test classes share one host and one database, so they run in the
/// <see cref="ApiCollection"/> (serially) and start from an empty database.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory) : IntegrationTestBase<ApiFactory, Program>(factory)
{
    /// <summary>Matches the API's own minimal-API serializer settings (web defaults: camelCase, case-insensitive).</summary>
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>An HTTP client with no credentials.</summary>
    protected HttpClient AnonymousClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    /// <summary>
    /// Creates a user and signs them in through the real <c>POST /api/auth/login</c>, so every
    /// authenticated test also exercises token issuance and validation.
    /// </summary>
    protected async Task<ApiUser> SignInNewUserAsync(string? email = null, string firstName = "Test", params string[] roles)
    {
        var user = await Data.CreateUserAsync(email, firstName: firstName,
            roles: roles.Length == 0 ? [ApplicationRoles.User] : roles);
        var login = await LoginAsync(user.Email!, TestData.DefaultPassword);

        var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        return new ApiUser(user, client, login);
    }

    protected Task<ApiUser> SignInNewAdminAsync() => SignInNewUserAsync(roles: ApplicationRoles.Admin);

    protected async Task<LoginResponse> LoginAsync(string email, string password)
    {
        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            DeviceName = "integration-tests",
        });

        await AssertStatusAsync(HttpStatusCode.OK, response);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
    }

    /// <summary>Sends and accepts a friend request through the API. Returns the friendship id.</summary>
    protected static async Task<long> MakeFriendsAsync(ApiUser requester, ApiUser addressee)
    {
        var sent = await requester.Client.PostAsJsonAsync("/api/friends/requests",
            new SendFriendRequestRequest { AddresseeUserId = addressee.Id });
        var friendshipId = await ReadAsync<long>(sent, HttpStatusCode.Created);

        await AssertStatusAsync(HttpStatusCode.NoContent,
            await addressee.Client.PutAsync($"/api/friends/requests/{friendshipId}/accept", null));

        return friendshipId;
    }

    /// <summary>Registers a push device for the user, so notifications to them reach <c>FakeApnsClient</c>.</summary>
    protected static async Task RegisterDeviceAsync(ApiUser user, string? token = null)
    {
        var response = await user.Client.PostAsJsonAsync("/api/devices",
            new RegisterDeviceRequest { DeviceToken = token ?? $"device-{Guid.NewGuid():N}", DeviceName = "iPhone" });
        await AssertStatusAsync(HttpStatusCode.NoContent, response);
    }

    /// <summary>Asserts a status code, putting the response body in the failure message.</summary>
    protected static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected} from {response.RequestMessage?.Method} " +
                        $"{response.RequestMessage?.RequestUri?.PathAndQuery} but got " +
                        $"{(int)response.StatusCode} {response.StatusCode}.\n{body}");
        }
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await AssertStatusAsync(expected, response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected static async Task<T> GetAsync<T>(HttpClient client, string url) =>
        await ReadAsync<T>(await client.GetAsync(url));

    protected static async Task<JsonElement> GetJsonAsync(HttpClient client, string url) =>
        await ReadAsync<JsonElement>(await client.GetAsync(url));
}
