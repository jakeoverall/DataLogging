namespace DataLogging.MockHardware.Models;

public sealed record MockDeviceDescriptor
{
    public required string DeviceId { get; init; }

    public required string Name { get; init; }

    public required string DeviceType { get; init; }

    public required DeviceProtocol Protocol { get; init; }

    public string? Topic { get; init; }

    public int? NodeId { get; init; }

    public string? Address { get; init; }
}
