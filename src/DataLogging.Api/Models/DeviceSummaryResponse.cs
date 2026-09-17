using DataLogging.Ingestion.Models;

namespace DataLogging.Api.Models;

public sealed record DeviceSummaryResponse
{
    public required string Id { get; init; }

    public required string DeviceId { get; init; }

    public required string Name { get; init; }

    public required string DeviceType { get; init; }

    public required DeviceProtocol Protocol { get; init; }

    public required string Status { get; init; }

    public required int Health { get; init; }

    public required string Address { get; init; }

    public required int Port { get; init; }

    public required bool Enabled { get; init; }

    public required string LastSeen { get; init; }

    public required string LastValue { get; init; }

    public required int AlertCount { get; init; }
}
