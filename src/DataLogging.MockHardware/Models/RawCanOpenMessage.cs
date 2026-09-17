namespace DataLogging.MockHardware.Models;

public sealed record RawCanOpenMessage
{
    public required string DeviceId { get; init; }

    public required string DeviceType { get; init; }

    public required string Name { get; init; }

    public required int NodeId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required ulong SequenceNumber { get; init; }

    public required CanOpenPayload Payload { get; init; }
}
