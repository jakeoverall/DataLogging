using DataLogging.MockHardware.Models;

namespace DataLogging.MockHardware.Abstractions;

public interface IMockDeviceRegistry
{
    void Register(MockDeviceDescriptor device);

    bool TryGet(string deviceId, out MockDeviceStateSnapshot? state);

    IReadOnlyCollection<MockDeviceDescriptor> GetDevices();

    IReadOnlyCollection<MockDeviceStateSnapshot> GetDeviceStates();

    IAsyncEnumerable<DeviceConnectionEvent> ReadConnectionEventsAsync(CancellationToken cancellationToken = default);

    void SetOnline(string deviceId, bool online, DateTimeOffset timestamp);

    void RecordEmission(string deviceId, DateTimeOffset timestamp, ulong sequenceNumber);

    void RecordDrop(string deviceId, DateTimeOffset timestamp);
}
