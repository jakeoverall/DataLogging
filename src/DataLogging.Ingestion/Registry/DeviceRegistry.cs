using System.Collections.Concurrent;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Registry;

public sealed class DeviceRegistry : IDeviceRegistry
{
    private readonly ConcurrentDictionary<string, DeviceDefinition> _devices = new();

    public void Register(DeviceDefinition device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_devices.TryAdd(device.DeviceId, device))
        {
            throw new InvalidOperationException(
                $"A device with ID '{device.DeviceId}' is already registered.");
        }
    }

    public bool Remove(string deviceId)
    {
        return _devices.TryRemove(deviceId, out _);
    }

    public bool TryGet(string deviceId, out DeviceDefinition? device)
    {
        return _devices.TryGetValue(deviceId, out device);
    }

    public IReadOnlyCollection<DeviceDefinition> GetAll()
    {
        return _devices.Values.ToArray();
    }
}
