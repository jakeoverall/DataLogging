namespace DataLogging.MockHardware.Models;

public sealed record RawRos2Message
{
    public required string DeviceId { get; init; }

    public required string DeviceType { get; init; }

    public required string Name { get; init; }

    public required string Topic { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required ulong SequenceNumber { get; init; }

    public required Ros2Payload Payload { get; init; }
}
