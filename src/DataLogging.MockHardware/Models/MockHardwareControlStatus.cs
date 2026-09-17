namespace DataLogging.MockHardware.Models;

public sealed record MockHardwareControlStatus
{
    public required MockHardwareRunState State { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required string Reason { get; init; }
}
