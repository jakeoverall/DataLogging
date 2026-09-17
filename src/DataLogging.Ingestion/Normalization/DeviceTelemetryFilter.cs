using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DataLogging.Core.Models;
using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Normalization;

public sealed class DeviceTelemetryFilter
{
    private readonly ConcurrentDictionary<string, DeviceTelemetryState> _deviceStates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Persists first-seen payloads and meaningful state changes, while treating timestamp-only updates
    /// as idle until the device heartbeat interval elapses.
    /// </summary>
    public bool ShouldPersist(LogRecord<object> record, DeviceDefinition device)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(device);

        var policy = DeviceTelemetryPolicy.FromProperties(device.Properties);
        var state = _deviceStates.GetOrAdd(device.DeviceId, _ => new DeviceTelemetryState());
        var payloadSignature = BuildPayloadSignature(record.Payload);
        var currentValue = ExtractNumericValue(record.Payload);

        if (state.LastPersistedAt is null)
        {
            state.Update(record.Metadata.RecordedTimestamp, payloadSignature, currentValue);
            return true;
        }

        var elapsed = record.Metadata.RecordedTimestamp - state.LastPersistedAt;
        var heartbeatDue = elapsed >= policy.HeartbeatInterval;
        var idlePersistDue = elapsed >= policy.IdlePersistInterval;

        if (string.Equals(state.LastPayloadSignature, payloadSignature, StringComparison.Ordinal))
        {
            if (!idlePersistDue)
            {
                return false;
            }

            state.Update(record.Metadata.RecordedTimestamp, payloadSignature, currentValue);
            return true;
        }

        if (!currentValue.HasValue || !state.LastValue.HasValue)
        {
            state.Update(record.Metadata.RecordedTimestamp, payloadSignature, currentValue);
            return true;
        }

        var delta = Math.Abs(state.LastValue.Value - currentValue.Value);
        var threshold = Math.Max(Math.Max(policy.DeltaThreshold, 0d), Math.Max(policy.FaultTolerance, 0d));
        if (delta >= threshold)
        {
            state.Update(record.Metadata.RecordedTimestamp, payloadSignature, currentValue);
            return true;
        }

        if (heartbeatDue)
        {
            state.Update(record.Metadata.RecordedTimestamp, payloadSignature, currentValue);
            return true;
        }

        return false;
    }

    private static string BuildPayloadSignature(object payload)
    {
        return BuildValueSignature(payload);
    }

    private static string BuildValueSignature(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is JsonElement jsonElement)
        {
            return BuildJsonElementSignature(jsonElement);
        }

        if (value is JsonElement[] elements)
        {
            var buffer = new StringBuilder();
            buffer.Append('[');
            for (var index = 0; index < elements.Length; index++)
            {
                if (index > 0)
                {
                    buffer.Append(',');
                }

                buffer.Append(BuildJsonElementSignature(elements[index]));
            }

            buffer.Append(']');
            return buffer.ToString();
        }

        if (value is IDictionary<string, object> objectDictionary)
        {
            return BuildDictionarySignature(objectDictionary.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)));
        }

        if (value is IEnumerable<KeyValuePair<string, object>> dictionaryEntries)
        {
            return BuildDictionarySignature(dictionaryEntries.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)));
        }

        if (value is IEnumerable<object> arrayValues)
        {
            var values = arrayValues.ToArray();
            var buffer = new StringBuilder();
            buffer.Append('[');
            for (var index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    buffer.Append(',');
                }

                buffer.Append(BuildValueSignature(values[index]));
            }

            buffer.Append(']');
            return buffer.ToString();
        }

        if (value is string text)
        {
            return JsonSerializer.Serialize(text);
        }

        if (value is DateTimeOffset dto)
        {
            return dto.ToString("O", CultureInfo.InvariantCulture);
        }

        if (value is DateTime dt)
        {
            return dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        return JsonSerializer.Serialize(value);
    }

    private static string BuildDictionarySignature(IEnumerable<KeyValuePair<string, object?>> values)
    {
        var filtered = values
            .Where(entry => !IsTimestampKey(entry.Key))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        var buffer = new StringBuilder();
        buffer.Append('{');
        for (var index = 0; index < filtered.Length; index++)
        {
            if (index > 0)
            {
                buffer.Append(',');
            }

            var entry = filtered[index];
            buffer.Append(JsonSerializer.Serialize(entry.Key));
            buffer.Append(':');
            buffer.Append(BuildValueSignature(entry.Value));
        }

        buffer.Append('}');
        return buffer.ToString();
    }

    private static string BuildJsonElementSignature(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject()
                .Where(property => !IsTimestampKey(property.Name))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();

            var buffer = new StringBuilder();
            buffer.Append('{');
            for (var index = 0; index < properties.Length; index++)
            {
                if (index > 0)
                {
                    buffer.Append(',');
                }

                var property = properties[index];
                buffer.Append(JsonSerializer.Serialize(property.Name));
                buffer.Append(':');
                buffer.Append(BuildJsonElementSignature(property.Value));
            }

            buffer.Append('}');
            return buffer.ToString();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var items = element.EnumerateArray().ToArray();
            var buffer = new StringBuilder();
            buffer.Append('[');
            for (var index = 0; index < items.Length; index++)
            {
                if (index > 0)
                {
                    buffer.Append(',');
                }

                buffer.Append(BuildJsonElementSignature(items[index]));
            }

            buffer.Append(']');
            return buffer.ToString();
        }

        return element.GetRawText();
    }

    private static bool IsTimestampKey(string key)
    {
        var normalized = key.Trim();
        return normalized.Equals("timestamp", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("time", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("recordedTimestamp", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("sourceTimestamp", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("lastUpdated", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("updatedAt", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("createdAt", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Timestamp", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Time", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("At", StringComparison.OrdinalIgnoreCase);
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

        public string LastPayloadSignature { get; private set; } = string.Empty;

        public double? LastValue { get; private set; }

        public void Update(DateTimeOffset timestamp, string payloadSignature, double? value)
        {
            LastPersistedAt = timestamp;
            LastPayloadSignature = payloadSignature;
            LastValue = value;
        }
    }
}