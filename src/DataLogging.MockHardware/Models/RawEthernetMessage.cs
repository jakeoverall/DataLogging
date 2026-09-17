namespace DataLogging.MockHardware.Models;

public sealed record RawEthernetMessage
{
    public required string DeviceId { get; init; }

    public required string DeviceType { get; init; }

    public required string Name { get; init; }

    public required string Address { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required ulong SequenceNumber { get; init; }

    public required EthernetPayload Payload { get; init; }
}
