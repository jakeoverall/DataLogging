using System.Text.Json;
using DataLogging.Core.Models;
using DataLogging.Ingestion.Models;
using DataLogging.Ingestion.Normalization;
using Xunit;

namespace DataLogging.Ingestion.Tests;

public sealed class DeviceTelemetryPolicyTests
{
    [Fact]
    public void DeviceTelemetryPolicy_Parses_Configuration_From_Device_Properties()
    {
        var device = new DeviceDefinition
        {
            DeviceId = "device-001",
            Name = "IMU",
            DeviceType = "IMU",
            Protocol = DeviceProtocol.Ros2,
            Properties = new Dictionary<string, string>
            {
                ["deltaThreshold"] = "0.25",
                ["faultTolerance"] = "0.75",
                ["heartbeatInterval"] = "15s"
            }
        };

        var policy = DeviceTelemetryPolicy.FromProperties(device.Properties);

        Assert.Equal(0.25, policy.DeltaThreshold);
        Assert.Equal(0.75, policy.FaultTolerance);
        Assert.Equal(TimeSpan.FromSeconds(15), policy.HeartbeatInterval);
    }

    [Fact]
    public void DeviceTelemetryFilter_Only_Persists_Meaningful_Changes_And_Heartbeat()
    {
        var filter = new DeviceTelemetryFilter();
        var device = new DeviceDefinition
        {
            DeviceId = "device-001",
            Name = "IMU",
            DeviceType = "IMU",
            Protocol = DeviceProtocol.Ros2,
            Properties = new Dictionary<string, string>
            {
                ["deltaThreshold"] = "0.10",
                ["faultTolerance"] = "0.25",
                ["heartbeatInterval"] = "1s"
            }
        };

        var first = CreateRecord(10.0, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        Assert.True(filter.ShouldPersist(first, device));

        var smallDelta = CreateRecord(10.04, DateTimeOffset.Parse("2026-01-01T00:00:00.500Z"));
        Assert.False(filter.ShouldPersist(smallDelta, device));

        var meaningfulDelta = CreateRecord(10.35, DateTimeOffset.Parse("2026-01-01T00:00:01.500Z"));
        Assert.True(filter.ShouldPersist(meaningfulDelta, device));

        var sameValue = CreateRecord(10.35, DateTimeOffset.Parse("2026-01-01T00:00:02.000Z"));
        Assert.False(filter.ShouldPersist(sameValue, device));

        var heartbeat = CreateRecord(10.35, DateTimeOffset.Parse("2026-01-01T00:00:03.500Z"));
        Assert.True(filter.ShouldPersist(heartbeat, device));
    }

    private static LogRecord<object> CreateRecord(double value, DateTimeOffset timestamp)
    {
        return new LogRecord<object>
        {
            Metadata = new LogRecordMetadata
            {
                RecordId = Guid.NewGuid(),
                VehicleId = "vehicle-1",
                DeviceId = "device-001",
                Source = "mock-ros2",
                DataType = "imu",
                Schema = "imu",
                RecordedTimestamp = timestamp,
                SequenceNumber = 1,
                Kind = LogRecordKind.Telemetry,
                Priority = LogPriority.Normal
            },
            Payload = new JsonElement[] { JsonDocument.Parse($"{{\"value\":{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}").RootElement }
        };
    }
}
