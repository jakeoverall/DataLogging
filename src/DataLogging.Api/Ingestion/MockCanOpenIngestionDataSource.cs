using System.Runtime.CompilerServices;
using System.Text.Json;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;
using DataLogging.MockHardware.Abstractions;

namespace DataLogging.Api.Ingestion;

public sealed class MockCanOpenIngestionDataSource : IDataSource
{
    private readonly IMockCanOpenSource _source;

    public MockCanOpenIngestionDataSource(IMockCanOpenSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    public string SourceId => "mock-canopen";

    public DeviceProtocol Protocol => DeviceProtocol.CanOpen;

    public async IAsyncEnumerable<RawData> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in _source.ReadAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            yield return new RawData
            {
                DeviceId = message.DeviceId,
                Protocol = DeviceProtocol.CanOpen,
                DataType = message.Payload.MessageType,
                Payload = JsonSerializer.SerializeToUtf8Bytes(message.Payload),
                SourceTimestamp = message.Timestamp,
                SequenceNumber = message.SequenceNumber,
                ReceivedTimestamp = DateTimeOffset.UtcNow
            };
        }
    }
}
