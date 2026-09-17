namespace DataLogging.Core.Models;

public sealed record LogRecordMetadata
{
    public required Guid RecordId { get; init; }

    public required string VehicleId { get; init; }

    public string? MissionId { get; init; }

    public string? DeviceId { get; init; }

    public required string Source { get; init; }

    public required string DataType { get; init; }

    public required string Schema { get; init; }

    public int SchemaVersion { get; init; } = 1;

    public DateTimeOffset? SourceTimestamp { get; init; }

    public required DateTimeOffset RecordedTimestamp { get; init; }

    public ulong? SequenceNumber { get; init; }

    public LogRecordKind Kind { get; init; }

    public LogPriority Priority { get; init; } = LogPriority.Normal;
}
