namespace DataLogging.Ingestion.Models;

public sealed record RawData
{
    public required string DeviceId { get; init; }

    public required DeviceProtocol Protocol { get; init; }

    public required string DataType { get; init; }

    public required ReadOnlyMemory<byte> Payload { get; init; }

    public DateTimeOffset? SourceTimestamp { get; init; }

    public ulong? SequenceNumber { get; init; }

    public DateTimeOffset ReceivedTimestamp { get; init; } = DateTimeOffset.UtcNow;
}
