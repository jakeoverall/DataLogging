using DataLogging.MockHardware.Configuration;
using DataLogging.MockHardware.Models;
using DataLogging.MockHardware.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DataLogging.MockHardware.Tests;

public sealed class MockHardwareSimulationTests
{
    [Fact]
    public async Task Simulator_Produces_MultiProtocolMessages_And_ShutsDownCleanly()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "3";
            settings["EmitFrequency"] = "50ms";
            settings["Duration"] = "300ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        var ros2 = host.Services.GetRequiredService<IMockRos2Source>();
        var canOpen = host.Services.GetRequiredService<IMockCanOpenSource>();
        var ethernet = host.Services.GetRequiredService<IMockEthernetSource>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var ros2Message = await ReadFirstAsync(ros2.ReadAsync(cts.Token), cts.Token);
        var canOpenMessage = await ReadFirstAsync(canOpen.ReadAsync(cts.Token), cts.Token);
        var ethernetMessage = await ReadFirstAsync(ethernet.ReadAsync(cts.Token), cts.Token);

        Assert.Equal("device-001", ros2Message.DeviceId);
        Assert.NotNull(canOpenMessage.Payload);
        Assert.NotNull(ethernetMessage.Payload);

        await host.StopAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task SequenceNumbers_AreMonotonic_PerDevice()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "1";
            settings["EmitFrequency"] = "25ms";
            settings["Duration"] = "120ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        var ros2 = host.Services.GetRequiredService<IMockRos2Source>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        await using var enumerator = ros2.ReadAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        Assert.True(await enumerator.MoveNextAsync());
        var first = enumerator.Current;
        Assert.True(await enumerator.MoveNextAsync());
        var second = enumerator.Current;

        Assert.Equal(1UL, first.SequenceNumber);
        Assert.Equal(2UL, second.SequenceNumber);

        await host.StopAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Timestamps_AreUtc_And_Increase()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "1";
            settings["EmitFrequency"] = "25ms";
            settings["Duration"] = "120ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        var ros2 = host.Services.GetRequiredService<IMockRos2Source>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        await using var enumerator = ros2.ReadAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        Assert.True(await enumerator.MoveNextAsync());
        var first = enumerator.Current;
        Assert.True(await enumerator.MoveNextAsync());
        var second = enumerator.Current;

        Assert.Equal(TimeSpan.Zero, first.Timestamp.Offset);
        Assert.True(second.Timestamp >= first.Timestamp);

        await host.StopAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task GeneratedValues_StayWithinBounds()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "10";
            settings["EmitFrequency"] = "25ms";
            settings["Duration"] = "250ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        var ros2 = host.Services.GetRequiredService<IMockRos2Source>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        BatteryRos2Payload? battery = null;
        ForkPositionRos2Payload? fork = null;

        await foreach (var message in ros2.ReadAsync(cts.Token).WithCancellation(cts.Token))
        {
            if (message.Payload is BatteryRos2Payload batteryPayload)
            {
                battery = batteryPayload;
            }

            if (message.Payload is ForkPositionRos2Payload forkPayload)
            {
                fork = forkPayload;
            }

            if (battery is not null && fork is not null)
            {
                break;
            }
        }

        Assert.NotNull(battery);
        Assert.InRange(battery!.StateOfCharge, 0, 100);
        Assert.NotNull(fork);
        Assert.InRange(fork!.HeightMeters, 0, 4.5);

        await host.StopAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Registry_ExposesStableUniqueDeviceIds()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "3";
            settings["EmitFrequency"] = "50ms";
            settings["Duration"] = "200ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        var registry = host.Services.GetRequiredService<MockDeviceRegistry>();
        var devices = await WaitForDevicesAsync(registry, expectedCount: 3, timeout: TimeSpan.FromSeconds(3));

        Assert.Equal(3, devices.Count);
        Assert.Equal(new[] { "device-001", "device-002", "device-003" }, devices.Select(device => device.DeviceId));

        await host.StopAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Simulation_ShutsDown_WhenCancelled()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "3";
            settings["EmitFrequency"] = "50ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        await host.StopAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task SameSeed_ProducesRepeatableFirstSample()
    {
        var firstRun = await CaptureFirstRos2PayloadAsync();
        var secondRun = await CaptureFirstRos2PayloadAsync();

        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public async Task ControlCommands_StopAndResumeEmissions()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "1";
            settings["EmitFrequency"] = "25ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        var registry = host.Services.GetRequiredService<MockDeviceRegistry>();
        var controller = host.Services.GetRequiredService<IMockHardwareController>();

        var initial = await WaitForSequenceAsync(registry, "device-001", timeout: TimeSpan.FromSeconds(3));
        controller.Stop();
        var pausedSequence = await WaitForSequenceAsync(registry, "device-001", timeout: TimeSpan.FromSeconds(1));
        await Task.Delay(200);
        var afterDelay = await WaitForSequenceAsync(registry, "device-001", timeout: TimeSpan.FromSeconds(1));

        Assert.Equal(pausedSequence, afterDelay);

        controller.Start();
        var resumed = await WaitForSequenceGreaterThanAsync(registry, "device-001", pausedSequence, TimeSpan.FromSeconds(3));
        Assert.True(resumed > initial);

        await host.StopAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Custom_Duration_Strings_Are_Parsed_Without_Default_TimeSpan_Binder_Failure()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NDevices"] = "3",
                ["EmitFrequency"] = "150ms",
                ["Duration"] = "1s",
                ["Seed"] = "42"
            })
            .Build();

        var options = new MockHardwareOptions();
        new MockHardwareOptionsSetup(config).Configure(options);

        Assert.Equal(TimeSpan.FromMilliseconds(150), options.EmitFrequency);
        Assert.Equal(TimeSpan.FromSeconds(1), options.Duration);
    }

    [Fact]
    public async Task Each_Device_Emits_At_Least_Ten_Messages_Before_The_Simulation_Ends()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "3";
            settings["EmitFrequency"] = "50ms";
            settings["Duration"] = "600ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        var ros2 = host.Services.GetRequiredService<IMockRos2Source>();
        var canOpen = host.Services.GetRequiredService<IMockCanOpenSource>();
        var ethernet = host.Services.GetRequiredService<IMockEthernetSource>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var ros2Count = await CountMessagesForDeviceAsync(ros2.ReadAsync(cts.Token), "device-001", cts.Token);
        var canCount = await CountMessagesForDeviceAsync(canOpen.ReadAsync(cts.Token), "device-002", cts.Token);
        var ethernetCount = await CountMessagesForDeviceAsync(ethernet.ReadAsync(cts.Token), "device-003", cts.Token);

        Assert.True(ros2Count >= 10, $"Expected at least 10 ROS2 logs for device-001 but got {ros2Count}.");
        Assert.True(canCount >= 10, $"Expected at least 10 CANOpen logs for device-002 but got {canCount}.");
        Assert.True(ethernetCount >= 10, $"Expected at least 10 Ethernet logs for device-003 but got {ethernetCount}.");

        await host.StopAsync(TimeSpan.FromSeconds(3));
    }

    private static async Task<int> CountMessagesForDeviceAsync<T>(IAsyncEnumerable<T> source, string deviceId, CancellationToken cancellationToken)
    {
        var count = 0;

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            if (item is RawRos2Message ros2Message && ros2Message.DeviceId == deviceId)
            {
                count++;
            }
            else if (item is RawCanOpenMessage canMessage && canMessage.DeviceId == deviceId)
            {
                count++;
            }
            else if (item is RawEthernetMessage ethernetMessage && ethernetMessage.DeviceId == deviceId)
            {
                count++;
            }

            if (count >= 10)
            {
                return count;
            }
        }

        return count;
    }

    private static async Task<string> CaptureFirstRos2PayloadAsync()
    {
        using var host = MockHardwareTestHost.CreateHost(settings =>
        {
            settings["NDevices"] = "1";
            settings["EmitFrequency"] = "25ms";
            settings["Duration"] = "100ms";
            settings["Seed"] = "42";
        });

        await host.StartAsync();

        var ros2 = host.Services.GetRequiredService<IMockRos2Source>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var message = await ReadFirstAsync(ros2.ReadAsync(cts.Token), cts.Token);

        await host.StopAsync(TimeSpan.FromSeconds(3));

        return message.Payload switch
        {
            ImuRos2Payload imu => $"{imu.AccelerationX:0.000}|{imu.AccelerationY:0.000}|{imu.AccelerationZ:0.000}|{imu.GyroX:0.000}|{imu.GyroY:0.000}|{imu.GyroZ:0.000}",
            BatteryRos2Payload battery => $"{battery.Voltage:0.000}|{battery.Current:0.000}|{battery.StateOfCharge:0.000}|{battery.TemperatureCelsius:0.000}",
            WheelEncoderRos2Payload wheel => $"{wheel.LeftWheelRpm:0.000}|{wheel.RightWheelRpm:0.000}|{wheel.DistanceMeters:0.000}",
            ForkPositionRos2Payload fork => $"{fork.HeightMeters:0.000}|{fork.VelocityMetersPerSecond:0.000}",
            LidarRos2Payload lidar => $"{lidar.RangeMeters:0.000}|{lidar.AngleDegrees:0.000}|{lidar.ObjectCount}|{lidar.ClosestObjectDistanceMeters:0.000}",
            CameraRos2Payload camera => $"{camera.FrameNumber}|{camera.Width}|{camera.Height}|{camera.Format}|{camera.ExposureMicroseconds:0.000}",
            _ => message.Payload.PayloadType
        };
    }

    private static async Task<T> ReadFirstAsync<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            return item;
        }

        throw new InvalidOperationException("The source completed before producing a message.");
    }

    private static async Task<T> ReadSecondAsync<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        var seen = 0;

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            seen++;
            if (seen == 2)
            {
                return item;
            }
        }

        throw new InvalidOperationException("The source completed before producing a second message.");
    }

    private static async Task<IReadOnlyCollection<DataLogging.MockHardware.Models.MockDeviceDescriptor>> WaitForDevicesAsync(
        MockDeviceRegistry registry,
        int expectedCount,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var devices = registry.GetDevices();
            if (devices.Count >= expectedCount)
            {
                return devices;
            }

            await Task.Delay(25);
        }

        return registry.GetDevices();
    }

    private static async Task<ulong> WaitForSequenceAsync(MockDeviceRegistry registry, string deviceId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (registry.TryGet(deviceId, out var state) && state is not null && state.SequenceNumber > 0)
            {
                return state.SequenceNumber;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Timed out waiting for device sequence.");
    }

    private static async Task<ulong> WaitForSequenceGreaterThanAsync(
        MockDeviceRegistry registry,
        string deviceId,
        ulong floor,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (registry.TryGet(deviceId, out var state) && state is not null && state.SequenceNumber > floor)
            {
                return state.SequenceNumber;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Timed out waiting for sequence advancement.");
    }
}
