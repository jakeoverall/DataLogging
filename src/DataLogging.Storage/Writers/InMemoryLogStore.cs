using System.Collections.Concurrent;
using DataLogging.Core.Abstractions;
using DataLogging.Core.Models;
using DataLogging.Core.Queries;

namespace DataLogging.Storage.Writers;

public sealed class InMemoryLogStore : ILogWriter, ILogReader
{
    private const int MaxRetainedRecords = 2000;
    private readonly ConcurrentQueue<LogRecordEnvelope> _records = new();

    public ValueTask WriteAsync(LogRecordEnvelope record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        _records.Enqueue(record);

        while (_records.Count > MaxRetainedRecords)
        {
            _records.TryDequeue(out _);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<LogRecordEnvelope> ReadAsync(
        LogQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var record in _records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (MatchesQuery(record, query))
            {
                yield return record;
                await Task.Yield();
            }
        }
    }

    private static bool MatchesQuery(LogRecordEnvelope record, LogQuery query)
    {
        var metadata = record.Metadata;

        if (query.DeviceId is not null && !string.Equals(query.DeviceId, metadata.DeviceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.Source is not null && !string.Equals(query.Source, metadata.Source, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.DataType is not null && !string.Equals(query.DataType, metadata.DataType, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.Kind is { } kind && metadata.Kind != kind)
        {
            return false;
        }

        if (query.MinimumPriority is { } minPriority && metadata.Priority < minPriority)
        {
            return false;
        }

        if (query.StartTime is { } start && metadata.RecordedTimestamp < start)
        {
            return false;
        }

        if (query.EndTime is { } end && metadata.RecordedTimestamp > end)
        {
            return false;
        }

        return true;
    }
}
