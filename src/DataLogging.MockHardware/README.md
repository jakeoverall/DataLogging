# DataLogging.MockHardware

`DataLogging.MockHardware` is a standalone .NET console application that simulates forklift hardware and emits raw source-specific telemetry for the ingestion layer.

## Purpose

The project provides a realistic producer-side simulation for development and automated testing. It does **not** implement the API layer, web UI, or any ingestion normalization logic.

## Architecture

```text
                +----------------------+
                |  Mock Hardware Host  |
                +----------+-----------+
                           |
        +------------------+------------------+
        |                  |                  |
        v                  v                  v
    Mock ROS2         Mock CANOpen      Mock Ethernet
        |                  |                  |
        +------------------+------------------+
                           |
                    Raw Message Streams
                           |
                           v
                    Data Ingestion
```

The simulator exposes raw protocol messages directly through asynchronous streams. The ingestion layer is still responsible for normalization into the common logging model.

## Supported protocols

- ROS2-style devices
  - LiDAR
  - Camera
  - IMU
  - Wheel Encoder
  - Battery Monitor
  - Fork Position
- CANOpen-style devices
  - Drive Motor Controller
  - Steering Controller
  - Fork Controller
  - Battery Controller
- Ethernet-style devices
  - Safety Scanner
  - Industrial Sensor
  - Motor Controller

## CLI options

| Option | Description | Default |
|---|---|---:|
| `--n_devices` | Number of simulated devices | `3` |
| `--emit_frequency` | Per-device emission interval | `1s` |
| `--log_raw` | Print concise raw message logs | disabled |
| `--duration` | Stop after the specified duration | run until cancelled |
| `--seed` | Deterministic seed for repeatable behavior | non-deterministic |
| `--buffer_capacity` | Bounded channel capacity for each protocol stream | `64` |

### Notes

- `--log_raw` is intentionally opt-in because it can materially reduce throughput at higher device counts and frequencies.
- `--seed` makes the generated telemetry repeatable enough for automated tests and scenario reproduction.
- Buffering is bounded with a fixed-capacity channel using backpressure (`Wait`) so the simulator does not grow memory without bound.

## Example commands

```bash
dotnet run
dotnet run -- --n_devices 3 --emit_frequency 0.75s
dotnet run -- --n_devices 10 --emit_frequency 250ms
dotnet run -- --n_devices 3 --emit_frequency 1s --log_raw
dotnet run -- --n_devices 3 --emit_frequency 100ms --duration 10s --seed 42
```

## Sample output

```text
info: DataLogging.MockHardware.Simulation.MockHardwareSimulation[0]
      Mock hardware starting
info: DataLogging.MockHardware.Simulation.MockHardwareSimulation[0]
      Configured devices: 3
info: DataLogging.MockHardware.Simulation.MockHardwareSimulation[0]
      Emit frequency: 00:00:01
info: DataLogging.MockHardware.Simulation.MockHardwareSimulation[0]
      Device device-001 started as IMU (Ros2)
info: DataLogging.MockHardware.Simulation.MockHardwareSimulation[0]
      Device device-002 started as Drive Motor Controller (CanOpen)
info: DataLogging.MockHardware.Simulation.MockHardwareSimulation[0]
      Device device-003 started as Safety Scanner (Ethernet)
info: DataLogging.MockHardware.Simulation.MockHardwareSimulation[0]
      Mock hardware running
```

When `--log_raw` is enabled, raw emission lines are also logged in a concise form, for example:

```text
info: DataLogging.MockHardware.Simulation.MockHardwareSimulation[0]
      [Ros2] device-001 seq=1 ts=2026-09-16T19:35:32.3620000Z imu accel=(0.02,-0.01,9.81)
```

## Testing

Run the project tests from the repository root:

```bash
dotnet test
```

The test suite covers configuration validation, device identity/sequence behavior, UTC timestamps, realistic value bounds, multi-protocol message delivery, cancellation, and deterministic seed behavior.
