namespace DataLogging.MockHardware.Models;

public sealed record DeviceConnectionEvent
{
    public required string DeviceId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required bool IsOnline { get; init; }

    public required string Reason { get; init; }
}
