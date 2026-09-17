using System.Collections.Concurrent;
using System.Threading.Channels;
using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Models;

namespace DataLogging.MockHardware.Registry;

public sealed class MockDeviceRegistry : IMockDeviceRegistry
{
    private readonly ConcurrentDictionary<string, MockDeviceDescriptor> _devices = new();
    private readonly ConcurrentDictionary<string, DeviceState> _states = new();
    private readonly Channel<DeviceConnectionEvent> _connectionEvents = Channel.CreateUnbounded<DeviceConnectionEvent>();

    public void Register(MockDeviceDescriptor device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_devices.TryAdd(device.DeviceId, device))
        {
            throw new InvalidOperationException($"A device with ID '{device.DeviceId}' is already registered.");
        }

        if (!_states.TryAdd(device.DeviceId, new DeviceState(device.DeviceId)))
        {
            throw new InvalidOperationException($"A state entry for device '{device.DeviceId}' already exists.");
        }
    }

    public bool TryGet(string deviceId, out MockDeviceStateSnapshot? state)
    {
        if (_states.TryGetValue(deviceId, out var deviceState))
        {
            state = deviceState.Snapshot();
            return true;
        }

        state = null;
        return false;
    }

    public IReadOnlyCollection<MockDeviceDescriptor> GetDevices()
    {
        return _devices.Values.OrderBy(device => device.DeviceId).ToArray();
    }

    public IReadOnlyCollection<MockDeviceStateSnapshot> GetDeviceStates()
    {
        return _states.Values
            .Select(state => state.Snapshot())
            .OrderBy(state => state.DeviceId)
            .ToArray();
    }

    public IAsyncEnumerable<DeviceConnectionEvent> ReadConnectionEventsAsync(CancellationToken cancellationToken = default)
    {
        return _connectionEvents.Reader.ReadAllAsync(cancellationToken);
    }

    public void SetOnline(string deviceId, bool online, DateTimeOffset timestamp)
    {
        if (!_states.TryGetValue(deviceId, out var state))
        {
            throw new KeyNotFoundException($"Device '{deviceId}' is not registered.");
        }

        state.SetOnline(online);
        _connectionEvents.Writer.TryWrite(new DeviceConnectionEvent
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            IsOnline = online,
            Reason = online ? "connected" : "disconnected"
        });
    }

    public void RecordEmission(string deviceId, DateTimeOffset timestamp, ulong sequenceNumber)
    {
        if (!_states.TryGetValue(deviceId, out var state))
        {
            throw new KeyNotFoundException($"Device '{deviceId}' is not registered.");
        }

        state.RecordEmission(timestamp, sequenceNumber);
    }

    public void RecordDrop(string deviceId, DateTimeOffset timestamp)
    {
        if (!_states.TryGetValue(deviceId, out var state))
        {
            throw new KeyNotFoundException($"Device '{deviceId}' is not registered.");
        }

        state.RecordDrop(timestamp);
    }

    private sealed class DeviceState
    {
        private readonly object _gate = new();

        private bool _isOnline;
        private long _messagesEmitted;
        private long _messagesDropped;
        private DateTimeOffset? _lastEmissionTimestamp;
        private ulong _sequenceNumber;

        public DeviceState(string deviceId)
        {
            DeviceId = deviceId;
        }

        public string DeviceId { get; }

        public void SetOnline(bool online)
        {
            lock (_gate)
            {
                _isOnline = online;
            }
        }

        public void RecordEmission(DateTimeOffset timestamp, ulong sequenceNumber)
        {
            lock (_gate)
            {
                _isOnline = true;
                _messagesEmitted++;
                _lastEmissionTimestamp = timestamp;
                _sequenceNumber = sequenceNumber;
            }
        }

        public void RecordDrop(DateTimeOffset timestamp)
        {
            lock (_gate)
            {
                _messagesDropped++;
                _lastEmissionTimestamp ??= timestamp;
            }
        }

        public MockDeviceStateSnapshot Snapshot()
        {
            lock (_gate)
            {
                return new MockDeviceStateSnapshot
                {
                    DeviceId = DeviceId,
                    IsOnline = _isOnline,
                    MessagesEmitted = _messagesEmitted,
                    MessagesDropped = _messagesDropped,
                    LastEmissionTimestamp = _lastEmissionTimestamp,
                    SequenceNumber = _sequenceNumber
                };
            }
        }
    }
}
