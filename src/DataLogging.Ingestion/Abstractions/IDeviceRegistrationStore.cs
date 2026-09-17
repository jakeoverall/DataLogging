using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Abstractions;

public interface IDeviceRegistrationStore
{
    Task<IReadOnlyCollection<DeviceDefinition>> LoadAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(DeviceDefinition device, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string deviceId, CancellationToken cancellationToken = default);
}
