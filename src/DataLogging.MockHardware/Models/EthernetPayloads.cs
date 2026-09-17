namespace DataLogging.MockHardware.Models;

public sealed record EthernetPayload
{
    public required string MessageType { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyDictionary<string, string> Fields { get; init; }
}
