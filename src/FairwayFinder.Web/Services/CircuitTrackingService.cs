using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Security.Claims;

namespace FairwayFinder.Web.Services;

public class ActiveCircuit
{
    public string CircuitId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan Duration => DateTime.UtcNow - ConnectedAt;
}

public class CircuitTrackingService
{
    private readonly ConcurrentDictionary<string, ActiveCircuit> _circuits = new();

    public IReadOnlyCollection<ActiveCircuit> GetActiveCircuits() => _circuits.Values.ToList().AsReadOnly();

    public int ActiveCount => _circuits.Count;

    internal void Add(string circuitId, string userName, string userEmail)
    {
        _circuits[circuitId] = new ActiveCircuit
        {
            CircuitId = circuitId,
            UserName = userName,
            UserEmail = userEmail,
            ConnectedAt = DateTime.UtcNow
        };
    }

    internal void Remove(string circuitId) => _circuits.TryRemove(circuitId, out _);
}

public class TrackingCircuitHandler(CircuitTrackingService trackingService, IHttpContextAccessor httpContextAccessor) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var userName = user?.FindFirstValue(ClaimTypes.Name) ?? "Anonymous";
        var userEmail = user?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        trackingService.Add(circuit.Id, userName, userEmail);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        trackingService.Remove(circuit.Id);
        return Task.CompletedTask;
    }
}
