namespace DataLogging.MockHardware.Models;

public sealed record MockDeviceStateSnapshot
{
    public required string DeviceId { get; init; }

    public required bool IsOnline { get; init; }

    public required long MessagesEmitted { get; init; }

    public required long MessagesDropped { get; init; }

    public required DateTimeOffset? LastEmissionTimestamp { get; init; }

    public required ulong SequenceNumber { get; init; }
}
