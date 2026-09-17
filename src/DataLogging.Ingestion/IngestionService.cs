using System.Runtime.CompilerServices;
using DataLogging.Core.Models;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;
using DataLogging.Ingestion.Normalization;

namespace DataLogging.Ingestion;

public sealed class IngestionService
{
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IDataNormalizer _normalizer;
    private readonly DeviceTelemetryFilter _telemetryFilter;

    public IngestionService(
        IDeviceRegistry deviceRegistry,
        IDataNormalizer normalizer,
        DeviceTelemetryFilter telemetryFilter)
    {
        _deviceRegistry = deviceRegistry;
        _normalizer = normalizer;
        _telemetryFilter = telemetryFilter;
    }

    public async IAsyncEnumerable<LogRecord<object>> IngestAsync(
        IDataSource source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var rawData in source.ReadAsync(cancellationToken))
        {
            if (!_deviceRegistry.TryGet(rawData.DeviceId, out var device))
            {
                continue;
            }

            if (device is null || !device.Enabled)
            {
                continue;
            }

            if (!_normalizer.CanNormalize(rawData, device))
            {
                continue;
            }

            var record = _normalizer.Normalize(rawData, device);
            if (!_telemetryFilter.ShouldPersist(record, device))
            {
                continue;
            }

            yield return record;
        }
    }
}
