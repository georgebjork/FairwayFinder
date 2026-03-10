using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Security.Claims;

namespace FairwayFinder.Web.Services;

public enum CircuitConnectionState
{
    /// <summary>Circuit is open and the browser is actively connected.</summary>
    Connected,
    /// <summary>Browser disconnected; waiting for reconnect (up to the server timeout).</summary>
    Disconnected
}

public class ActiveCircuit
{
    public string CircuitId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan Duration => DateTime.UtcNow - ConnectedAt;
    public CircuitConnectionState ConnectionState { get; set; } = CircuitConnectionState.Connected;
    public DateTime? DisconnectedAt { get; set; }
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

    internal void MarkDisconnected(string circuitId)
    {
        if (_circuits.TryGetValue(circuitId, out var circuit))
        {
            circuit.ConnectionState = CircuitConnectionState.Disconnected;
            circuit.DisconnectedAt = DateTime.UtcNow;
        }
    }

    internal void MarkReconnected(string circuitId)
    {
        if (_circuits.TryGetValue(circuitId, out var circuit))
        {
            circuit.ConnectionState = CircuitConnectionState.Connected;
            circuit.DisconnectedAt = null;
        }
    }

    // Called when the server finally disposes the circuit after the reconnect timeout
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

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        trackingService.MarkDisconnected(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        trackingService.MarkReconnected(circuit.Id);
        return Task.CompletedTask;
    }

    // Fires after the reconnect window expires — circuit is truly gone
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        trackingService.Remove(circuit.Id);
        return Task.CompletedTask;
    }
}
