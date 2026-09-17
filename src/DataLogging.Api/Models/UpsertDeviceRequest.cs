using DataLogging.Ingestion.Models;

namespace DataLogging.Api.Models;

public sealed record UpsertDeviceRequest
{
    public required string Name { get; init; }

    public required string DeviceType { get; init; }

    public required DeviceProtocol Protocol { get; init; }

    public bool Enabled { get; init; } = true;

    public string? Address { get; init; }

    public string? Port { get; init; }

    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}
