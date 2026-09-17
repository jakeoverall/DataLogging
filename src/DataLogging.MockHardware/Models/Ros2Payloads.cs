namespace DataLogging.MockHardware.Models;

public abstract record Ros2Payload
{
    public required string PayloadType { get; init; }
}

public sealed record ImuRos2Payload : Ros2Payload
{
    public required double AccelerationX { get; init; }

    public required double AccelerationY { get; init; }

    public required double AccelerationZ { get; init; }

    public required double GyroX { get; init; }

    public required double GyroY { get; init; }

    public required double GyroZ { get; init; }
}

public sealed record WheelEncoderRos2Payload : Ros2Payload
{
    public required double LeftWheelRpm { get; init; }

    public required double RightWheelRpm { get; init; }

    public required double DistanceMeters { get; init; }
}

public sealed record BatteryRos2Payload : Ros2Payload
{
    public required double Voltage { get; init; }

    public required double Current { get; init; }

    public required double StateOfCharge { get; init; }

    public required double TemperatureCelsius { get; init; }
}

public sealed record ForkPositionRos2Payload : Ros2Payload
{
    public required double HeightMeters { get; init; }

    public required double VelocityMetersPerSecond { get; init; }
}

public sealed record LidarRos2Payload : Ros2Payload
{
    public required double RangeMeters { get; init; }

    public required double AngleDegrees { get; init; }

    public required int ObjectCount { get; init; }

    public required double ClosestObjectDistanceMeters { get; init; }

    public required double[] RepresentativeRanges { get; init; }
}

public sealed record CameraRos2Payload : Ros2Payload
{
    public required ulong FrameNumber { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required string Format { get; init; }

    public required double ExposureMicroseconds { get; init; }
}
