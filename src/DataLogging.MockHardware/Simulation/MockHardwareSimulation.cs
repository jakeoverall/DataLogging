using System.Globalization;
using System.Security.Cryptography;
using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Configuration;
using DataLogging.MockHardware.Models;
using DataLogging.MockHardware.Registry;
using DataLogging.MockHardware.Sources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataLogging.MockHardware.Simulation;

public sealed class MockHardwareSimulation
{
    private readonly MockHardwareOptions _options;
    private readonly MockDeviceRegistry _registry;
    private readonly MockRos2Source _ros2Source;
    private readonly MockCanOpenSource _canOpenSource;
    private readonly MockEthernetSource _ethernetSource;
    private readonly IMockHardwareController _controller;
    private readonly ILogger<MockHardwareSimulation> _logger;
    private readonly TimeProvider _timeProvider;

    public MockHardwareSimulation(
        IOptions<MockHardwareOptions> options,
        MockDeviceRegistry registry,
        MockRos2Source ros2Source,
        MockCanOpenSource canOpenSource,
        MockEthernetSource ethernetSource,
        IMockHardwareController controller,
        ILogger<MockHardwareSimulation> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(ros2Source);
        ArgumentNullException.ThrowIfNull(canOpenSource);
        ArgumentNullException.ThrowIfNull(ethernetSource);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options.Value;
        _registry = registry;
        _ros2Source = ros2Source;
        _canOpenSource = canOpenSource;
        _ethernetSource = ethernetSource;
        _controller = controller;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var catalog = DeviceCatalog.Create(_options.NDevices);
        var seed = _options.Seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var deviceTasks = new List<Task>(catalog.Count);

        try
        {
            _logger.LogInformation("Mock hardware starting");
            _logger.LogInformation("Configured devices: {DeviceCount}", _options.NDevices);
            _logger.LogInformation("Emit frequency: {EmitFrequency}", _options.EmitFrequency);

            foreach (var device in catalog)
            {
                _registry.Register(device.Descriptor);
                _registry.SetOnline(device.Descriptor.DeviceId, true, _timeProvider.GetUtcNow());
                _logger.LogInformation(
                    "Device {DeviceId} started as {DeviceType} ({Protocol})",
                    device.Descriptor.DeviceId,
                    device.Descriptor.DeviceType,
                    device.Descriptor.Protocol);
            }

            if (_options.Duration is { } duration)
            {
                linkedCts.CancelAfter(duration);
                _logger.LogInformation("Simulation duration: {Duration}", duration);
            }

            foreach (var device in catalog)
            {
                var deviceSeed = unchecked(seed + device.Index * 7919);
                deviceTasks.Add(RunDeviceAsync(device, deviceSeed, linkedCts.Token));
            }

            _logger.LogInformation("Mock hardware running");
            await Task.WhenAll(deviceTasks).ConfigureAwait(false);
        }
        finally
        {
            linkedCts.Cancel();
            _ros2Source.Complete();
            _canOpenSource.Complete();
            _ethernetSource.Complete();
        }
    }

    private async Task RunDeviceAsync(DeviceCatalogEntry device, int seed, CancellationToken cancellationToken)
    {
        var random = new Random(seed);
        var sequenceNumber = 0UL;
        var isOnline = true;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var timestamp = _timeProvider.GetUtcNow();
                var status = _controller.GetStatus();
                if (status.State == MockHardwareRunState.Stopped)
                {
                    if (isOnline)
                    {
                        _registry.SetOnline(device.Descriptor.DeviceId, false, timestamp);
                        isOnline = false;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!isOnline)
                {
                    _registry.SetOnline(device.Descriptor.DeviceId, true, timestamp);
                    isOnline = true;
                }

                if (status.State == MockHardwareRunState.Paused)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                sequenceNumber++;
                await EmitAsync(device, timestamp, sequenceNumber, random, cancellationToken).ConfigureAwait(false);
                _registry.RecordEmission(device.Descriptor.DeviceId, timestamp, sequenceNumber);

                if (_options.LogRaw)
                {
                    _logger.LogInformation(
                        "[{Protocol}] {DeviceId} seq={SequenceNumber} ts={Timestamp:o} {Summary}",
                        device.Descriptor.Protocol,
                        device.Descriptor.DeviceId,
                        sequenceNumber,
                        timestamp,
                        device.LastPayloadSummary);
                }

                await Task.Delay(_options.EmitFrequency, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (isOnline)
            {
                _registry.SetOnline(device.Descriptor.DeviceId, false, _timeProvider.GetUtcNow());
            }
        }
    }

    private async Task EmitAsync(
        DeviceCatalogEntry device,
        DateTimeOffset timestamp,
        ulong sequenceNumber,
        Random random,
        CancellationToken cancellationToken)
    {
        switch (device.Descriptor.Protocol)
        {
            case DeviceProtocol.Ros2:
                await EmitRos2Async(device, timestamp, sequenceNumber, random, cancellationToken).ConfigureAwait(false);
                break;
            case DeviceProtocol.CanOpen:
                await EmitCanOpenAsync(device, timestamp, sequenceNumber, random, cancellationToken).ConfigureAwait(false);
                break;
            case DeviceProtocol.Ethernet:
                await EmitEthernetAsync(device, timestamp, sequenceNumber, random, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported protocol '{device.Descriptor.Protocol}'.");
        }
    }

    private async Task EmitRos2Async(
        DeviceCatalogEntry device,
        DateTimeOffset timestamp,
        ulong sequenceNumber,
        Random random,
        CancellationToken cancellationToken)
    {
        var message = device.Ros2Kind switch
        {
            Ros2DeviceKind.Imu => new RawRos2Message
            {
                DeviceId = device.Descriptor.DeviceId,
                DeviceType = device.Descriptor.DeviceType,
                Name = device.Descriptor.Name,
                Topic = device.Descriptor.Topic ?? "/topic/imu",
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                Payload = new ImuRos2Payload
                {
                    PayloadType = "imu",
                    AccelerationX = device.ImuState.NextAccelerationX(random),
                    AccelerationY = device.ImuState.NextAccelerationY(random),
                    AccelerationZ = device.ImuState.NextAccelerationZ(random),
                    GyroX = device.ImuState.NextGyroX(random),
                    GyroY = device.ImuState.NextGyroY(random),
                    GyroZ = device.ImuState.NextGyroZ(random)
                }
            },
            Ros2DeviceKind.WheelEncoder => new RawRos2Message
            {
                DeviceId = device.Descriptor.DeviceId,
                DeviceType = device.Descriptor.DeviceType,
                Name = device.Descriptor.Name,
                Topic = device.Descriptor.Topic ?? "/topic/wheel_encoder",
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                Payload = new WheelEncoderRos2Payload
                {
                    PayloadType = "wheel_encoder",
                    LeftWheelRpm = device.WheelState.NextLeftRpm(random),
                    RightWheelRpm = device.WheelState.NextRightRpm(random),
                    DistanceMeters = device.WheelState.AdvanceDistance(_options.EmitFrequency)
                }
            },
            Ros2DeviceKind.BatteryMonitor => new RawRos2Message
            {
                DeviceId = device.Descriptor.DeviceId,
                DeviceType = device.Descriptor.DeviceType,
                Name = device.Descriptor.Name,
                Topic = device.Descriptor.Topic ?? "/topic/battery",
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                Payload = new BatteryRos2Payload
                {
                    PayloadType = "battery",
                    Voltage = device.BatteryState.NextVoltage(random),
                    Current = device.BatteryState.NextCurrent(random),
                    StateOfCharge = device.BatteryState.NextStateOfCharge(_options.EmitFrequency),
                    TemperatureCelsius = device.BatteryState.NextTemperature(random)
                }
            },
            Ros2DeviceKind.ForkPosition => new RawRos2Message
            {
                DeviceId = device.Descriptor.DeviceId,
                DeviceType = device.Descriptor.DeviceType,
                Name = device.Descriptor.Name,
                Topic = device.Descriptor.Topic ?? "/topic/fork_position",
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                Payload = new ForkPositionRos2Payload
                {
                    PayloadType = "fork_position",
                    HeightMeters = device.ForkState.NextHeight(random, _options.EmitFrequency),
                    VelocityMetersPerSecond = device.ForkState.NextVelocity(random)
                }
            },
            Ros2DeviceKind.Lidar => new RawRos2Message
            {
                DeviceId = device.Descriptor.DeviceId,
                DeviceType = device.Descriptor.DeviceType,
                Name = device.Descriptor.Name,
                Topic = device.Descriptor.Topic ?? "/topic/lidar",
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                Payload = new LidarRos2Payload
                {
                    PayloadType = "lidar",
                    RangeMeters = device.LidarState.NextRange(random),
                    AngleDegrees = device.LidarState.NextAngle(random),
                    ObjectCount = device.LidarState.NextObjectCount(random),
                    ClosestObjectDistanceMeters = device.LidarState.NextClosestObjectDistance(random),
                    RepresentativeRanges = device.LidarState.NextRepresentativeRanges(random)
                }
            },
            Ros2DeviceKind.Camera => new RawRos2Message
            {
                DeviceId = device.Descriptor.DeviceId,
                DeviceType = device.Descriptor.DeviceType,
                Name = device.Descriptor.Name,
                Topic = device.Descriptor.Topic ?? "/topic/camera",
                Timestamp = timestamp,
                SequenceNumber = sequenceNumber,
                Payload = new CameraRos2Payload
                {
                    PayloadType = "camera",
                    FrameNumber = device.CameraState.NextFrameNumber(),
                    Width = 1280,
                    Height = 720,
                    Format = "Mono8",
                    ExposureMicroseconds = device.CameraState.NextExposure(random)
                }
            },
            _ => throw new InvalidOperationException($"Unsupported ROS2 device kind '{device.Ros2Kind}'.")
        };

        await _ros2Source.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        device.LastPayloadSummary = Describe(message.Payload);
    }

    private async Task EmitCanOpenAsync(
        DeviceCatalogEntry device,
        DateTimeOffset timestamp,
        ulong sequenceNumber,
        Random random,
        CancellationToken cancellationToken)
    {
        var payload = device.CanOpenKind switch
        {
            CanOpenDeviceKind.DriveMotorController => new NumericCanOpenPayload
            {
                MessageType = "drive_motor_status",
                NodeId = device.Descriptor.NodeId ?? 1,
                ObjectIndex = 0x606C,
                SubIndex = 0,
                Value = device.CanOpenState.NextDriveMotorRpm(random),
                Unit = "rpm"
            },
            CanOpenDeviceKind.SteeringController => new NumericCanOpenPayload
            {
                MessageType = "steering_angle",
                NodeId = device.Descriptor.NodeId ?? 2,
                ObjectIndex = 0x6064,
                SubIndex = 0,
                Value = device.CanOpenState.NextSteeringAngle(random),
                Unit = "deg"
            },
            CanOpenDeviceKind.ForkController => new NumericCanOpenPayload
            {
                MessageType = "fork_height",
                NodeId = device.Descriptor.NodeId ?? 3,
                ObjectIndex = 0x6063,
                SubIndex = 0,
                Value = device.CanOpenState.NextForkHeight(random),
                Unit = "m"
            },
            CanOpenDeviceKind.BatteryController => new NumericCanOpenPayload
            {
                MessageType = "battery_soc",
                NodeId = device.Descriptor.NodeId ?? 4,
                ObjectIndex = 0x6001,
                SubIndex = 0,
                Value = device.CanOpenState.NextBatterySoc(_options.EmitFrequency),
                Unit = "%"
            },
            _ => throw new InvalidOperationException($"Unsupported CANOpen device kind '{device.CanOpenKind}'.")
        };

        var message = new RawCanOpenMessage
        {
            DeviceId = device.Descriptor.DeviceId,
            DeviceType = device.Descriptor.DeviceType,
            Name = device.Descriptor.Name,
            NodeId = payload.NodeId,
            Timestamp = timestamp,
            SequenceNumber = sequenceNumber,
            Payload = payload
        };

        await _canOpenSource.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        device.LastPayloadSummary = Describe(payload);
    }

    private async Task EmitEthernetAsync(
        DeviceCatalogEntry device,
        DateTimeOffset timestamp,
        ulong sequenceNumber,
        Random random,
        CancellationToken cancellationToken)
    {
        var payload = device.EthernetKind switch
        {
            EthernetDeviceKind.SafetyScanner => new EthernetPayload
            {
                MessageType = "SafetyState",
                Summary = "safety scanner state",
                Fields = new Dictionary<string, string>
                {
                    ["fault"] = device.EthernetState.NextFaultState(random),
                    ["scan_rate_hz"] = device.EthernetState.NextScanRate(random).ToString("0.0", CultureInfo.InvariantCulture)
                }
            },
            EthernetDeviceKind.IndustrialSensor => new EthernetPayload
            {
                MessageType = "SensorReading",
                Summary = "industrial sensor reading",
                Fields = new Dictionary<string, string>
                {
                    ["temperature_c"] = device.EthernetState.NextTemperature(random).ToString("0.0", CultureInfo.InvariantCulture),
                    ["pressure_kpa"] = device.EthernetState.NextPressure(random).ToString("0.0", CultureInfo.InvariantCulture)
                }
            },
            EthernetDeviceKind.MotorController => new EthernetPayload
            {
                MessageType = "MotorTelemetry",
                Summary = "motor controller telemetry",
                Fields = new Dictionary<string, string>
                {
                    ["rpm"] = device.EthernetState.NextRpm(random).ToString("0", CultureInfo.InvariantCulture),
                    ["temperature_c"] = device.EthernetState.NextTemperature(random).ToString("0.0", CultureInfo.InvariantCulture)
                }
            },
            _ => throw new InvalidOperationException($"Unsupported Ethernet device kind '{device.EthernetKind}'.")
        };

        var message = new RawEthernetMessage
        {
            DeviceId = device.Descriptor.DeviceId,
            DeviceType = device.Descriptor.DeviceType,
            Name = device.Descriptor.Name,
            Address = device.Descriptor.Address ?? "10.0.0.1",
            Timestamp = timestamp,
            SequenceNumber = sequenceNumber,
            Payload = payload
        };

        await _ethernetSource.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        device.LastPayloadSummary = Describe(payload);
    }

    private static string Describe(Ros2Payload payload)
    {
        return payload switch
        {
            ImuRos2Payload imu => $"imu accel=({imu.AccelerationX:0.00},{imu.AccelerationY:0.00},{imu.AccelerationZ:0.00})",
            WheelEncoderRos2Payload wheel => $"wheel rpm=({wheel.LeftWheelRpm:0.0},{wheel.RightWheelRpm:0.0}) distance={wheel.DistanceMeters:0.00}",
            BatteryRos2Payload battery => $"battery soc={battery.StateOfCharge:0.0}% voltage={battery.Voltage:0.0}V",
            ForkPositionRos2Payload fork => $"fork height={fork.HeightMeters:0.00}m velocity={fork.VelocityMetersPerSecond:0.00}m/s",
            LidarRos2Payload lidar => $"lidar range={lidar.RangeMeters:0.0}m objects={lidar.ObjectCount}",
            CameraRos2Payload camera => $"camera frame={camera.FrameNumber} {camera.Width}x{camera.Height} {camera.Format}",
            _ => payload.PayloadType
        };
    }

    private static string Describe(CanOpenPayload payload)
    {
        return payload switch
        {
            NumericCanOpenPayload numeric => $"{numeric.MessageType} node={numeric.NodeId} index=0x{numeric.ObjectIndex:X4} value={numeric.Value:0.0}",
            BooleanCanOpenPayload boolean => $"{boolean.MessageType} node={boolean.NodeId} index=0x{boolean.ObjectIndex:X4} value={boolean.Value}",
            TextCanOpenPayload text => $"{text.MessageType} node={text.NodeId} index=0x{text.ObjectIndex:X4} value={text.Value}",
            _ => payload.MessageType
        };
    }

    private static string Describe(EthernetPayload payload)
    {
        return $"{payload.MessageType} {payload.Summary}";
    }

    private sealed class DeviceCatalogEntry
    {
        public required int Index { get; init; }

        public required MockDeviceDescriptor Descriptor { get; init; }

        public required Ros2DeviceKind? Ros2Kind { get; init; }

        public required CanOpenDeviceKind? CanOpenKind { get; init; }

        public required EthernetDeviceKind? EthernetKind { get; init; }

        public required ImuSignalState ImuState { get; init; }

        public required WheelSignalState WheelState { get; init; }

        public required BatterySignalState BatteryState { get; init; }

        public required ForkSignalState ForkState { get; init; }

        public required LidarSignalState LidarState { get; init; }

        public required CameraSignalState CameraState { get; init; }

        public required CanOpenSignalState CanOpenState { get; init; }

        public required EthernetSignalState EthernetState { get; init; }

        public string? LastPayloadSummary { get; set; }
    }

    private static class DeviceCatalog
    {
        public static IReadOnlyList<DeviceCatalogEntry> Create(int nDevices)
        {
            var entries = new List<DeviceCatalogEntry>(nDevices);

            for (var index = 0; index < nDevices; index++)
            {
                entries.Add(CreateEntry(index));
            }

            return entries;
        }

        private static DeviceCatalogEntry CreateEntry(int index)
        {
            var catalogIndex = index % 13;
            var deviceId = $"device-{index + 1:D3}";

            return catalogIndex switch
            {
                0 => Ros2Entry(index, deviceId, Ros2DeviceKind.Imu, "IMU", "/topic/imu"),
                1 => CanOpenEntry(index, deviceId, CanOpenDeviceKind.DriveMotorController, "Drive Motor Controller", 4),
                2 => EthernetEntry(index, deviceId, EthernetDeviceKind.SafetyScanner, "Safety Scanner", $"10.0.0.{120 + index}"),
                3 => Ros2Entry(index, deviceId, Ros2DeviceKind.WheelEncoder, "Wheel Encoder", "/topic/wheel_encoder"),
                4 => CanOpenEntry(index, deviceId, CanOpenDeviceKind.SteeringController, "Steering Controller", 5),
                5 => EthernetEntry(index, deviceId, EthernetDeviceKind.IndustrialSensor, "Industrial Sensor", $"10.0.0.{130 + index}"),
                6 => Ros2Entry(index, deviceId, Ros2DeviceKind.BatteryMonitor, "Battery Monitor", "/topic/battery"),
                7 => CanOpenEntry(index, deviceId, CanOpenDeviceKind.ForkController, "Fork Controller", 6),
                8 => EthernetEntry(index, deviceId, EthernetDeviceKind.MotorController, "Motor Controller", $"10.0.0.{140 + index}"),
                9 => Ros2Entry(index, deviceId, Ros2DeviceKind.ForkPosition, "Fork Position", "/topic/fork_position"),
                10 => Ros2Entry(index, deviceId, Ros2DeviceKind.Lidar, "LiDAR", "/topic/lidar"),
                11 => Ros2Entry(index, deviceId, Ros2DeviceKind.Camera, "Camera", "/topic/camera"),
                _ => CanOpenEntry(index, deviceId, CanOpenDeviceKind.BatteryController, "Battery Controller", 7)
            };
        }

        private static DeviceCatalogEntry Ros2Entry(int index, string deviceId, Ros2DeviceKind kind, string name, string topic)
        {
            return new DeviceCatalogEntry
            {
                Index = index,
                Descriptor = new MockDeviceDescriptor
                {
                    DeviceId = deviceId,
                    Name = name,
                    DeviceType = name,
                    Protocol = DeviceProtocol.Ros2,
                    Topic = topic
                },
                Ros2Kind = kind,
                CanOpenKind = null,
                EthernetKind = null,
                ImuState = new ImuSignalState(),
                WheelState = new WheelSignalState(),
                BatteryState = new BatterySignalState(),
                ForkState = new ForkSignalState(),
                LidarState = new LidarSignalState(),
                CameraState = new CameraSignalState(),
                CanOpenState = new CanOpenSignalState(),
                EthernetState = new EthernetSignalState()
            };
        }

        private static DeviceCatalogEntry CanOpenEntry(int index, string deviceId, CanOpenDeviceKind kind, string name, int nodeId)
        {
            return new DeviceCatalogEntry
            {
                Index = index,
                Descriptor = new MockDeviceDescriptor
                {
                    DeviceId = deviceId,
                    Name = name,
                    DeviceType = name,
                    Protocol = DeviceProtocol.CanOpen,
                    NodeId = nodeId
                },
                Ros2Kind = null,
                CanOpenKind = kind,
                EthernetKind = null,
                ImuState = new ImuSignalState(),
                WheelState = new WheelSignalState(),
                BatteryState = new BatterySignalState(),
                ForkState = new ForkSignalState(),
                LidarState = new LidarSignalState(),
                CameraState = new CameraSignalState(),
                CanOpenState = new CanOpenSignalState(),
                EthernetState = new EthernetSignalState()
            };
        }

        private static DeviceCatalogEntry EthernetEntry(int index, string deviceId, EthernetDeviceKind kind, string name, string address)
        {
            return new DeviceCatalogEntry
            {
                Index = index,
                Descriptor = new MockDeviceDescriptor
                {
                    DeviceId = deviceId,
                    Name = name,
                    DeviceType = name,
                    Protocol = DeviceProtocol.Ethernet,
                    Address = address
                },
                Ros2Kind = null,
                CanOpenKind = null,
                EthernetKind = kind,
                ImuState = new ImuSignalState(),
                WheelState = new WheelSignalState(),
                BatteryState = new BatterySignalState(),
                ForkState = new ForkSignalState(),
                LidarState = new LidarSignalState(),
                CameraState = new CameraSignalState(),
                CanOpenState = new CanOpenSignalState(),
                EthernetState = new EthernetSignalState()
            };
        }
    }

    private enum Ros2DeviceKind
    {
        Imu,
        WheelEncoder,
        BatteryMonitor,
        ForkPosition,
        Lidar,
        Camera
    }

    private enum CanOpenDeviceKind
    {
        DriveMotorController,
        SteeringController,
        ForkController,
        BatteryController
    }

    private enum EthernetDeviceKind
    {
        SafetyScanner,
        IndustrialSensor,
        MotorController
    }

    private sealed class ImuSignalState
    {
        private double _accelX = 0.01;
        private double _accelY = -0.02;
        private double _accelZ = 0.04;
        private double _gyroX;
        private double _gyroY;
        private double _gyroZ;

        public double NextAccelerationX(Random random) => _accelX = Clamp(_accelX + Noise(random, 0.05), -2.0, 2.0);

        public double NextAccelerationY(Random random) => _accelY = Clamp(_accelY + Noise(random, 0.05), -2.0, 2.0);

        public double NextAccelerationZ(Random random) => _accelZ = Clamp(_accelZ + Noise(random, 0.05), 9.45, 10.15);

        public double NextGyroX(Random random) => _gyroX = Clamp(_gyroX + Noise(random, 0.04), -0.5, 0.5);

        public double NextGyroY(Random random) => _gyroY = Clamp(_gyroY + Noise(random, 0.04), -0.5, 0.5);

        public double NextGyroZ(Random random) => _gyroZ = Clamp(_gyroZ + Noise(random, 0.04), -0.5, 0.5);
    }

    private sealed class WheelSignalState
    {
        private double _leftRpm = 1050;
        private double _rightRpm = 1045;
        private double _distance;

        public double NextLeftRpm(Random random)
        {
            return _leftRpm = Clamp(_leftRpm + Noise(random, 20), 0, 2400);
        }

        public double NextRightRpm(Random random)
        {
            return _rightRpm = Clamp(_rightRpm + Noise(random, 20), 0, 2400);
        }

        public double AdvanceDistance(TimeSpan frequency)
        {
            var averageRpm = (_leftRpm + _rightRpm) / 2.0;
            var revolutionsPerSecond = averageRpm / 60.0;
            _distance += revolutionsPerSecond * frequency.TotalSeconds * 0.5;
            return _distance;
        }
    }

    private sealed class BatterySignalState
    {
        private double _voltage = 48.2;
        private double _current = 14.1;
        private double _soc = 88.0;
        private double _temperature = 26.2;

        public double NextVoltage(Random random) => _voltage = Clamp(_voltage + Noise(random, 0.08), 44.0, 52.0);

        public double NextCurrent(Random random) => _current = Clamp(_current + Noise(random, 0.3), -50.0, 50.0);

        public double NextStateOfCharge(TimeSpan frequency) => _soc = Clamp(_soc - frequency.TotalSeconds * 0.002, 0.0, 100.0);

        public double NextTemperature(Random random) => _temperature = Clamp(_temperature + Noise(random, 0.1), -20.0, 60.0);
    }

    private sealed class ForkSignalState
    {
        private double _height = 1.2;
        private double _velocity;

        public double NextHeight(Random random, TimeSpan frequency)
        {
            _velocity = Clamp(_velocity + Noise(random, 0.05), -0.6, 0.6);
            _height = Clamp(_height + _velocity * frequency.TotalSeconds, 0.0, 4.5);

            if (_height <= 0.0 || _height >= 4.5)
            {
                _velocity = -_velocity * 0.7;
            }

            return _height;
        }

        public double NextVelocity(Random random) => Clamp(_velocity + Noise(random, 0.04), -0.6, 0.6);
    }

    private sealed class LidarSignalState
    {
        private double _range = 18.0;
        private double _angle;
        private int _objectCount = 2;
        private double _closest = 4.2;

        public double NextRange(Random random) => _range = Clamp(_range + Noise(random, 0.15), 0.5, 30.0);

        public double NextAngle(Random random)
        {
            _angle = (_angle + 7.5 + random.NextDouble() * 0.5) % 360.0;
            return _angle;
        }

        public int NextObjectCount(Random random)
        {
            _objectCount = Math.Clamp(_objectCount + random.Next(-1, 2), 0, 12);
            return _objectCount;
        }

        public double NextClosestObjectDistance(Random random) => _closest = Clamp(_closest + Noise(random, 0.1), 0.4, 25.0);

        public double[] NextRepresentativeRanges(Random random)
        {
            return
            [
                NextRange(random),
                Clamp(_range + Noise(random, 0.1), 0.5, 30.0),
                Clamp(_range + Noise(random, 0.1), 0.5, 30.0)
            ];
        }
    }

    private sealed class CameraSignalState
    {
        private ulong _frameNumber;
        private double _exposure = 6800;

        public ulong NextFrameNumber() => ++_frameNumber;

        public double NextExposure(Random random) => _exposure = Clamp(_exposure + Noise(random, 150), 4000, 12000);
    }

    private sealed class CanOpenSignalState
    {
        private double _driveMotorRpm = 1050;
        private double _steeringAngle;
        private double _forkHeight = 1.15;
        private double _batterySoc = 87.5;

        public double NextDriveMotorRpm(Random random) => _driveMotorRpm = Clamp(_driveMotorRpm + Noise(random, 30), -1200, 1200);

        public double NextSteeringAngle(Random random) => _steeringAngle = Clamp(_steeringAngle + Noise(random, 1.5), -35, 35);

        public double NextForkHeight(Random random) => _forkHeight = Clamp(_forkHeight + Noise(random, 0.05), 0, 4.5);

        public double NextBatterySoc(TimeSpan frequency) => _batterySoc = Clamp(_batterySoc - frequency.TotalSeconds * 0.002, 0, 100);
    }

    private sealed class EthernetSignalState
    {
        private bool _fault;
        private double _scanRate = 25.0;
        private double _temperature = 31.0;
        private double _pressure = 101.1;
        private double _rpm = 810.0;

        public string NextFaultState(Random random)
        {
            if (random.NextDouble() < 0.02)
            {
                _fault = !_fault;
            }

            return _fault ? "fault" : "ok";
        }

        public double NextScanRate(Random random) => _scanRate = Clamp(_scanRate + Noise(random, 0.2), 15.0, 40.0);

        public double NextTemperature(Random random) => _temperature = Clamp(_temperature + Noise(random, 0.12), -10.0, 80.0);

        public double NextPressure(Random random) => _pressure = Clamp(_pressure + Noise(random, 0.08), 80.0, 130.0);

        public double NextRpm(Random random) => _rpm = Clamp(_rpm + Noise(random, 12), 0.0, 1600.0);
    }

    private static double Noise(Random random, double amplitude)
    {
        return (random.NextDouble() - 0.5) * 2.0 * amplitude;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Clamp(value, min, max);
    }
}
