using System.Runtime.CompilerServices;
using System.Text.Json;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;
using DataLogging.MockHardware.Abstractions;

namespace DataLogging.Api.Ingestion;

public sealed class MockRos2IngestionDataSource : IDataSource
{
    private readonly IMockRos2Source _source;

    public MockRos2IngestionDataSource(IMockRos2Source source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    public string SourceId => "mock-ros2";

    public DeviceProtocol Protocol => DeviceProtocol.Ros2;

    public async IAsyncEnumerable<RawData> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in _source.ReadAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            yield return new RawData
            {
                DeviceId = message.DeviceId,
                Protocol = DeviceProtocol.Ros2,
                DataType = message.Payload.PayloadType,
                Payload = JsonSerializer.SerializeToUtf8Bytes(message.Payload),
                SourceTimestamp = message.Timestamp,
                SequenceNumber = message.SequenceNumber,
                ReceivedTimestamp = DateTimeOffset.UtcNow
            };
        }
    }
}
