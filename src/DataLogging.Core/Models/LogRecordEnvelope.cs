namespace DataLogging.Core.Models;

public sealed record LogRecordEnvelope
{
    public required LogRecordMetadata Metadata { get; init; }

    public required ReadOnlyMemory<byte> Payload { get; init; }
}
