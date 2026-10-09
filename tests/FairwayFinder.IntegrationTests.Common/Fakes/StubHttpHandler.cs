using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FairwayFinder.IntegrationTests.Common.Fakes;

/// <summary>
/// Stands in for third-party HTTP APIs (GolfCourseAPI, TGTR). Tests register canned responses by
/// path; anything unregistered is a 404, so a test never reaches the real internet.
/// </summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new();
    private readonly ConcurrentQueue<HttpRequestMessage> _requests = new();

    public IReadOnlyList<HttpRequestMessage> Requests => _requests.ToArray();

    /// <summary>Responds to requests whose path (no query string) matches <paramref name="path"/>.</summary>
    public void Respond(string path, Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _routes[path] = responder;

    public void RespondJson<T>(string path, T body, JsonSerializerOptions? options = null) =>
        Respond(path, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body, options: options ?? Web),
        });

    public void Clear()
    {
        _routes.Clear();
        _requests.Clear();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);

        var response = _routes.TryGetValue(request.RequestUri!.AbsolutePath, out var responder)
            ? responder(request)
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        response.RequestMessage = request;
        return Task.FromResult(response);
    }
}
