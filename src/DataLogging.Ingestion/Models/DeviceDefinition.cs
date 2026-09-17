namespace DataLogging.Ingestion.Models;

public sealed record DeviceDefinition
{
    public required string DeviceId { get; init; }

    public required string Name { get; init; }

    public required string DeviceType { get; init; }

    public required DeviceProtocol Protocol { get; init; }

    public string? Address { get; init; }

    public string? Port { get; init; }

    public bool Enabled { get; init; } = true;

    public IReadOnlyDictionary<string, string> Properties { get; init; }
        = new Dictionary<string, string>();
}
