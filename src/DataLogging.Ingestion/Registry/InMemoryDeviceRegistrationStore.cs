using System.Collections.Concurrent;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Registry;

public sealed class InMemoryDeviceRegistrationStore : IDeviceRegistrationStore
{
    private readonly ConcurrentDictionary<string, DeviceDefinition> _devices = new();

    public Task<IReadOnlyCollection<DeviceDefinition>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var values = _devices.Values.OrderBy(device => device.DeviceId).ToArray();
        return Task.FromResult<IReadOnlyCollection<DeviceDefinition>>(values);
    }

    public Task UpsertAsync(DeviceDefinition device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        _devices.AddOrUpdate(device.DeviceId, device, (_, _) => device);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_devices.TryRemove(deviceId, out _));
    }
}
