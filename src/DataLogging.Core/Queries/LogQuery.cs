using DataLogging.Core.Models;

namespace DataLogging.Core.Queries;

public sealed record LogQuery
{
    public string? VehicleId { get; init; }

    public string? MissionId { get; init; }

    public string? DeviceId { get; init; }

    public string? Source { get; init; }

    public string? DataType { get; init; }

    public LogRecordKind? Kind { get; init; }

    public LogPriority? MinimumPriority { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }

    public int? Limit { get; init; }
}
