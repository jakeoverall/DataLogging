namespace DataLogging.Api.Models;

public sealed record DeviceStreamSnapshotResponse
{
    public required DateTimeOffset Timestamp { get; init; }

    public required IReadOnlyCollection<DeviceSummaryResponse> Devices { get; init; }
}
