namespace DataLogging.MockHardware.Models;

public abstract record CanOpenPayload
{
    public required string MessageType { get; init; }

    public required int NodeId { get; init; }

    public required ushort ObjectIndex { get; init; }

    public required byte SubIndex { get; init; }
}

public sealed record NumericCanOpenPayload : CanOpenPayload
{
    public required double Value { get; init; }

    public string? Unit { get; init; }
}

public sealed record BooleanCanOpenPayload : CanOpenPayload
{
    public required bool Value { get; init; }
}

public sealed record TextCanOpenPayload : CanOpenPayload
{
    public required string Value { get; init; }
}
