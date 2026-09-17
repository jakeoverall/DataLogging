using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Registry;

public sealed class DeviceRegistrationService
{
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IDeviceRegistrationStore _store;

    public DeviceRegistrationService(
        IDeviceRegistry deviceRegistry,
        IDeviceRegistrationStore store)
    {
        ArgumentNullException.ThrowIfNull(deviceRegistry);
        ArgumentNullException.ThrowIfNull(store);

        _deviceRegistry = deviceRegistry;
        _store = store;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var devices = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);

        foreach (var device in devices)
        {
            _deviceRegistry.Upsert(device);
        }
    }

    public async Task UpsertAsync(DeviceDefinition device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        _deviceRegistry.Upsert(device);
        await _store.UpsertAsync(device, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var removedFromRegistry = _deviceRegistry.Remove(deviceId);
        var removedFromStore = await _store.RemoveAsync(deviceId, cancellationToken).ConfigureAwait(false);

        return removedFromRegistry || removedFromStore;
    }

    public IReadOnlyCollection<DeviceDefinition> GetAll()
    {
        return _deviceRegistry.GetAll();
    }
}
