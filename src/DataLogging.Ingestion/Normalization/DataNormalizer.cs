using System.Text.Json;
using DataLogging.Core.Models;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Normalization;

public sealed class DataNormalizer : IDataNormalizer
{
    public bool CanNormalize(RawData data, DeviceDefinition device)
    {
        return device.Protocol == data.Protocol;
    }

    public LogRecord<object> Normalize(RawData data, DeviceDefinition device)
    {
        if (!CanNormalize(data, device))
        {
            throw new InvalidOperationException(
                $"Device '{device.DeviceId}' cannot process data received from protocol '{data.Protocol}'.");
        }

        var payload = DeserializePayload(data);

        return new LogRecord<object>
        {
            Metadata = new LogRecordMetadata
            {
                RecordId = Guid.NewGuid(),
                VehicleId = "unknown",
                DeviceId = device.DeviceId,
                Source = data.Protocol.ToString(),
                DataType = data.DataType,
                Schema = data.DataType,
                SchemaVersion = 1,
                SourceTimestamp = data.SourceTimestamp,
                RecordedTimestamp = data.ReceivedTimestamp,
                SequenceNumber = data.SequenceNumber,
                Kind = LogRecordKind.Telemetry,
                Priority = LogPriority.Normal
            },
            Payload = payload
        };
    }

    private static object DeserializePayload(RawData data)
    {
        if (data.Payload.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        return JsonSerializer.Deserialize<JsonElement>(data.Payload.Span);
    }
}
