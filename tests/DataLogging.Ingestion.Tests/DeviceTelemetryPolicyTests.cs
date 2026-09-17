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
                ["heartbeatInterval"] = "15s",
                ["idlePersistInterval"] = "5m"
            }
        };

        var policy = DeviceTelemetryPolicy.FromProperties(device.Properties);

        Assert.Equal(0.25, policy.DeltaThreshold);
        Assert.Equal(0.75, policy.FaultTolerance);
        Assert.Equal(TimeSpan.FromSeconds(15), policy.HeartbeatInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.IdlePersistInterval);
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

    [Fact]
    public void DeviceTelemetryFilter_Treats_Timestamp_Only_Payload_Changes_As_Idle_Until_Heartbeat()
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
                ["heartbeatInterval"] = "2s"
            }
        };

        var baseline = CreateRecordWithPayload(
            "{\"status\":\"stable\",\"temperature\":22.5,\"timestamp\":\"2026-01-01T00:00:00Z\"}",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        Assert.True(filter.ShouldPersist(baseline, device));

        var timestampOnlyChange = CreateRecordWithPayload(
            "{\"status\":\"stable\",\"temperature\":22.5,\"timestamp\":\"2026-01-01T00:00:01Z\"}",
            DateTimeOffset.Parse("2026-01-01T00:00:01Z"));
        Assert.False(filter.ShouldPersist(timestampOnlyChange, device));

        var heartbeat = CreateRecordWithPayload(
            "{\"status\":\"stable\",\"temperature\":22.5,\"timestamp\":\"2026-01-01T00:00:03Z\"}",
            DateTimeOffset.Parse("2026-01-01T00:00:03Z"));
        Assert.True(filter.ShouldPersist(heartbeat, device));
    }

    [Fact]
    public void DeviceTelemetryFilter_Uses_Custom_Idle_Persist_Interval_Per_Device()
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
                ["heartbeatInterval"] = "1s",
                ["idlePersistInterval"] = "5s"
            }
        };

        var baseline = CreateRecordWithPayload(
            "{\"status\":\"stable\",\"temperature\":22.5,\"timestamp\":\"2026-01-01T00:00:00Z\"}",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        Assert.True(filter.ShouldPersist(baseline, device));

        var idleBeforeInterval = CreateRecordWithPayload(
            "{\"status\":\"stable\",\"temperature\":22.5,\"timestamp\":\"2026-01-01T00:00:03Z\"}",
            DateTimeOffset.Parse("2026-01-01T00:00:03Z"));
        Assert.False(filter.ShouldPersist(idleBeforeInterval, device));

        var idleAtInterval = CreateRecordWithPayload(
            "{\"status\":\"stable\",\"temperature\":22.5,\"timestamp\":\"2026-01-01T00:00:05Z\"}",
            DateTimeOffset.Parse("2026-01-01T00:00:05Z"));
        Assert.True(filter.ShouldPersist(idleAtInterval, device));
    }

    [Fact]
    public void DeviceTelemetryFilter_Persists_NonNumeric_State_Changes_Before_Heartbeat()
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
                ["heartbeatInterval"] = "15s"
            }
        };

        var first = CreateRecordWithPayload(
            "{\"mode\":\"manual\",\"timestamp\":\"2026-01-01T00:00:00Z\"}",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        Assert.True(filter.ShouldPersist(first, device));

        var modeChanged = CreateRecordWithPayload(
            "{\"mode\":\"auto\",\"timestamp\":\"2026-01-01T00:00:01Z\"}",
            DateTimeOffset.Parse("2026-01-01T00:00:01Z"));
        Assert.True(filter.ShouldPersist(modeChanged, device));
    }

    private static LogRecord<object> CreateRecord(double value, DateTimeOffset timestamp)
    {
        return CreateRecordWithPayload(
            $"{{\"value\":{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}",
            timestamp);
    }

    private static LogRecord<object> CreateRecordWithPayload(string payloadJson, DateTimeOffset timestamp)
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
            Payload = new JsonElement[] { JsonDocument.Parse(payloadJson).RootElement }
        };
    }
}
