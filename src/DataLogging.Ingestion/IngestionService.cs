using System.Runtime.CompilerServices;
using DataLogging.Core.Models;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion;

public sealed class IngestionService
{
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IDataNormalizer _normalizer;

    public IngestionService(
        IDeviceRegistry deviceRegistry,
        IDataNormalizer normalizer)
    {
        _deviceRegistry = deviceRegistry;
        _normalizer = normalizer;
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

            yield return _normalizer.Normalize(rawData, device);
        }
    }
}
