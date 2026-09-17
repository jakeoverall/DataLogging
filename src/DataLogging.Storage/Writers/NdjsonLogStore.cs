using System.Text.Json;
using DataLogging.Core.Abstractions;
using DataLogging.Core.Models;
using DataLogging.Core.Queries;
using DataLogging.Storage.Configuration;
using Microsoft.Extensions.Options;

namespace DataLogging.Storage.Writers;

public sealed class NdjsonLogStore : ILogWriter, ILogReader
{
    private readonly StorageOptions _options;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public NdjsonLogStore(IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public async ValueTask WriteAsync(LogRecordEnvelope record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_options.FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(record);
            await File.AppendAllTextAsync(_options.FilePath, line + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
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

        if (!File.Exists(_options.FilePath))
        {
            yield break;
        }

        await using var stream = File.OpenRead(_options.FilePath);
        using var reader = new StreamReader(stream);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<LogRecordEnvelope>(line)
                ?? throw new InvalidOperationException("Unable to deserialize persisted log record.");

            if (InMemoryLogStoreMatchesQuery(record, query))
            {
                yield return record;
            }
        }
    }

    private static bool InMemoryLogStoreMatchesQuery(LogRecordEnvelope record, LogQuery query)
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
