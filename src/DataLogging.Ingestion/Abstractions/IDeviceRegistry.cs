using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Abstractions;

public interface IDeviceRegistry
{
    void Register(DeviceDefinition device);

    void Upsert(DeviceDefinition device);

    bool Remove(string deviceId);

    bool TryGet(string deviceId, out DeviceDefinition? device);

    IReadOnlyCollection<DeviceDefinition> GetAll();
}
