namespace DataLogging.Core.Models;

public sealed record LogRecord<TPayload>
{
    public required LogRecordMetadata Metadata { get; init; }

    public required TPayload Payload { get; init; }
}
