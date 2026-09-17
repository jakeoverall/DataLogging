using System.Text.Json;
using DataLogging.Core.Abstractions;
using DataLogging.Core.Models;

namespace DataLogging.Storage.Serialization;

public sealed class JsonLogSerializer : ILogSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public LogRecordEnvelope Serialize<TPayload>(LogRecord<TPayload> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var payload = JsonSerializer.SerializeToUtf8Bytes(record.Payload, SerializerOptions);
        return new LogRecordEnvelope
        {
            Metadata = record.Metadata,
            Payload = payload
        };
    }

    public LogRecord<TPayload> Deserialize<TPayload>(LogRecordEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var payload = JsonSerializer.Deserialize<TPayload>(envelope.Payload.Span, SerializerOptions)
            ?? throw new InvalidOperationException("Unable to deserialize record payload.");

        return new LogRecord<TPayload>
        {
            Metadata = envelope.Metadata,
            Payload = payload
        };
    }
}
