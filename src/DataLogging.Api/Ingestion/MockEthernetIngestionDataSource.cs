using System.Runtime.CompilerServices;
using System.Text.Json;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;
using DataLogging.MockHardware.Abstractions;

namespace DataLogging.Api.Ingestion;

public sealed class MockEthernetIngestionDataSource : IDataSource
{
    private readonly IMockEthernetSource _source;

    public MockEthernetIngestionDataSource(IMockEthernetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    public string SourceId => "mock-ethernet";

    public DeviceProtocol Protocol => DeviceProtocol.Ethernet;

    public async IAsyncEnumerable<RawData> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in _source.ReadAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            yield return new RawData
            {
                DeviceId = message.DeviceId,
                Protocol = DeviceProtocol.Ethernet,
                DataType = message.Payload.MessageType,
                Payload = JsonSerializer.SerializeToUtf8Bytes(message.Payload),
                SourceTimestamp = message.Timestamp,
                SequenceNumber = message.SequenceNumber,
                ReceivedTimestamp = DateTimeOffset.UtcNow
            };
        }
    }
}
