using System.Collections.Concurrent;

namespace DataLogging.Api.Ingestion;

public sealed class WebSocketConnectionTracker
{
    private readonly ConcurrentDictionary<string, WebSocketConnectionState> _states = new(StringComparer.Ordinal);

    public void MarkConnected(string deviceId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        _states[deviceId] = new WebSocketConnectionState
        {
            IsConnected = true,
            LastSeen = at,
            LastConnectedAt = at
        };
    }

    public void MarkReceived(string deviceId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        _states[deviceId] = new WebSocketConnectionState
        {
            IsConnected = true,
            LastSeen = at,
            LastConnectedAt = _states.TryGetValue(deviceId, out var existing) ? existing.LastConnectedAt ?? at : at
        };
    }

    public void MarkDisconnected(string deviceId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        _states[deviceId] = new WebSocketConnectionState
        {
            IsConnected = false,
            LastSeen = _states.TryGetValue(deviceId, out var existing) ? existing.LastSeen ?? at : at,
            LastConnectedAt = _states.TryGetValue(deviceId, out var connected) ? connected.LastConnectedAt : null
        };
    }

    public bool IsConnected(string deviceId)
    {
        return _states.TryGetValue(deviceId, out var state) && state.IsConnected;
    }

    public DateTimeOffset? GetLastSeen(string deviceId)
    {
        return _states.TryGetValue(deviceId, out var state) ? state.LastSeen : null;
    }

    public IReadOnlyCollection<WebSocketConnectionState> GetAll()
    {
        return _states.Values.ToArray();
    }
}

public sealed record WebSocketConnectionState
{
    public bool IsConnected { get; init; }

    public DateTimeOffset? LastSeen { get; init; }

    public DateTimeOffset? LastConnectedAt { get; init; }
}
