using System.Collections.Concurrent;
using System.Text.Json;
using DataLogging.Core.Models;
using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Normalization;

public sealed class DeviceTelemetryFilter
{
    private readonly ConcurrentDictionary<string, DeviceTelemetryState> _deviceStates = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldPersist(LogRecord<object> record, DeviceDefinition device)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(device);

        var policy = DeviceTelemetryPolicy.FromProperties(device.Properties);
        var state = _deviceStates.GetOrAdd(device.DeviceId, _ => new DeviceTelemetryState());
        var currentValue = ExtractNumericValue(record.Payload);

        if (!currentValue.HasValue)
        {
            state.Update(record.Metadata.RecordedTimestamp, 0d);
            return true;
        }

        if (state.LastPersistedAt is null)
        {
            state.Update(record.Metadata.RecordedTimestamp, currentValue.Value);
            return true;
        }

        var heartbeatDue = record.Metadata.RecordedTimestamp - state.LastPersistedAt >= policy.HeartbeatInterval;
        if (heartbeatDue)
        {
            state.Update(record.Metadata.RecordedTimestamp, currentValue.Value);
            return true;
        }

        var delta = Math.Abs(state.LastValue - currentValue.Value);
        var threshold = Math.Max(Math.Max(policy.DeltaThreshold, 0d), Math.Max(policy.FaultTolerance, 0d));
        if (delta >= threshold)
        {
            state.Update(record.Metadata.RecordedTimestamp, currentValue.Value);
            return true;
        }

        return false;
    }

    private static double? ExtractNumericValue(object payload)
    {
        if (payload is JsonElement element)
        {
            return ExtractNumericValue(element);
        }

        if (payload is JsonElement[] elements)
        {
            foreach (var item in elements)
            {
                var value = ExtractNumericValue(item);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        if (payload is IEnumerable<KeyValuePair<string, object>> dictionary)
        {
            foreach (var pair in dictionary)
            {
                var value = ExtractNumericValue(pair.Value);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        if (payload is IDictionary<string, object> dictionaryValues)
        {
            foreach (var value in dictionaryValues.Values)
            {
                var numeric = ExtractNumericValue(value);
                if (numeric.HasValue)
                {
                    return numeric;
                }
            }

            return null;
        }

        return null;
    }

    private static double? ExtractNumericValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var extracted = ExtractNumericValue(property.Value);
                if (extracted.HasValue)
                {
                    return extracted.Value;
                }
            }

            return null;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var extracted = ExtractNumericValue(item);
                if (extracted.HasValue)
                {
                    return extracted.Value;
                }
            }

            return null;
        }

        if (element.ValueKind is JsonValueKind.Number && element.TryGetDouble(out var value))
        {
            return value;
        }

        return null;
    }

    private sealed class DeviceTelemetryState
    {
        public DateTimeOffset? LastPersistedAt { get; private set; }

        public double LastValue { get; private set; }

        public void Update(DateTimeOffset timestamp, double value)
        {
            LastPersistedAt = timestamp;
            LastValue = value;
        }
    }
}