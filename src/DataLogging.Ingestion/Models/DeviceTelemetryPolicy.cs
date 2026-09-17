namespace DataLogging.Ingestion.Models;

public sealed record DeviceTelemetryPolicy
{
    public double DeltaThreshold { get; init; } = 0d;

    public double FaultTolerance { get; init; } = 0d;

    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    public static DeviceTelemetryPolicy FromProperties(IReadOnlyDictionary<string, string>? properties)
    {
        var lookup = properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var delta = ReadDouble(lookup, ["delta", "deltaThreshold", "minDelta", "changeThreshold", "sampleDelta"]);
        var faultTolerance = ReadDouble(lookup, ["faultTolerance", "faultToleranceValue", "faultThreshold", "maxFaultDelta"]);
        var heartbeatInterval = ReadTimeSpan(lookup, ["heartbeatInterval", "heartbeatIntervalSeconds", "heartbeatSeconds", "heartbeat", "sampleInterval", "sampleIntervalSeconds"]);

        return new DeviceTelemetryPolicy
        {
            DeltaThreshold = delta,
            FaultTolerance = faultTolerance,
            HeartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30)
        };
    }

    private static double ReadDouble(IReadOnlyDictionary<string, string> properties, IEnumerable<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (properties.TryGetValue(alias, out var value) && TryParseDouble(value, out var parsed))
            {
                return parsed;
            }
        }

        return 0d;
    }

    private static TimeSpan? ReadTimeSpan(IReadOnlyDictionary<string, string> properties, IEnumerable<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (properties.TryGetValue(alias, out var value) && TryParseTimeSpan(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool TryParseDouble(string value, out double parsed)
    {
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryParseTimeSpan(string value, out TimeSpan parsed)
    {
        var trimmed = value.Trim();
        if (TimeSpan.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out parsed))
        {
            return true;
        }

        var normalized = trimmed.ToLowerInvariant();
        if (normalized.EndsWith("ms", StringComparison.Ordinal))
        {
            return double.TryParse(normalized[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var milliseconds)
                && (parsed = TimeSpan.FromMilliseconds(milliseconds)) >= TimeSpan.Zero;
        }

        if (normalized.EndsWith("s", StringComparison.Ordinal))
        {
            return double.TryParse(normalized[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                && (parsed = TimeSpan.FromSeconds(seconds)) >= TimeSpan.Zero;
        }

        if (normalized.EndsWith("m", StringComparison.Ordinal))
        {
            return double.TryParse(normalized[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minutes)
                && (parsed = TimeSpan.FromMinutes(minutes)) >= TimeSpan.Zero;
        }

        if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var secondsValue))
        {
            parsed = TimeSpan.FromSeconds(secondsValue);
            return parsed >= TimeSpan.Zero;
        }

        parsed = default;
        return false;
    }
}
