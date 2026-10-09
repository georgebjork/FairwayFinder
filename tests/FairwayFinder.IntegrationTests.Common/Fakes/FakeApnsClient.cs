using System.Collections.Concurrent;
using dotAPNS;

namespace FairwayFinder.IntegrationTests.Common.Fakes;

/// <summary>
/// Stands in for Apple's push gateway. The real <c>PushNotificationService</c> still runs —
/// device lookup, payload building, deactivating dead tokens — only the network hop is faked.
/// </summary>
public sealed class FakeApnsClient : IApnsClient
{
    private readonly ConcurrentQueue<ApplePush> _sent = new();

    public IReadOnlyList<ApplePush> Sent => _sent.ToArray();

    public void Clear() => _sent.Clear();

    public Task<ApnsResponse> Send(ApplePush push) => SendAsync(push);

    public Task<ApnsResponse> SendAsync(ApplePush push, CancellationToken ct = default)
    {
        _sent.Enqueue(push);
        return Task.FromResult(ApnsResponse.Successful());
    }
}
