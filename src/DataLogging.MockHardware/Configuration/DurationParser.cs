using System.Globalization;

namespace DataLogging.MockHardware.Configuration;

public static class DurationParser
{
    public static TimeSpan Parse(string value, string optionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (TryParseSuffixDuration(value, out var duration))
        {
            return duration;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out duration))
        {
            return duration;
        }

        throw new FormatException($"The {optionName} value '{value}' is not a valid duration. Use values such as 250ms, 0.75s, 1s, or 5m.");
    }

    private static bool TryParseSuffixDuration(string value, out TimeSpan duration)
    {
        duration = default;

        var normalized = value.Trim().ToLowerInvariant();
        var unit = normalized.EndsWith("ms", StringComparison.Ordinal)
            ? "ms"
            : normalized.EndsWith("s", StringComparison.Ordinal)
                ? "s"
                : normalized.EndsWith("m", StringComparison.Ordinal)
                    ? "m"
                    : normalized.EndsWith("h", StringComparison.Ordinal)
                        ? "h"
                        : string.Empty;

        if (string.IsNullOrEmpty(unit))
        {
            return false;
        }

        var numericPart = normalized[..^unit.Length];
        if (!double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity))
        {
            return false;
        }

        duration = unit switch
        {
            "ms" => TimeSpan.FromMilliseconds(quantity),
            "s" => TimeSpan.FromSeconds(quantity),
            "m" => TimeSpan.FromMinutes(quantity),
            "h" => TimeSpan.FromHours(quantity),
            _ => default
        };

        return duration > TimeSpan.Zero;
    }
}
