using System.Text.Json;
using System.Text;
using DataLogging.Core.Abstractions;
using DataLogging.Core.Models;
using DataLogging.Core.Queries;
using DataLogging.Storage.Configuration;
using Microsoft.Extensions.Options;

namespace DataLogging.Storage.Writers;

public sealed class NdjsonLogStore : ILogWriter, ILogReader
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
            var payload = line + Environment.NewLine;
            var payloadSize = Utf8.GetByteCount(payload);

            RotateIfNeeded(payloadSize);
            await File.AppendAllTextAsync(_options.FilePath, payload, Utf8, cancellationToken).ConfigureAwait(false);
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

    private void RotateIfNeeded(int nextWriteBytes)
    {
        if (_options.MaxFileSizeBytes <= 0 || !File.Exists(_options.FilePath))
        {
            return;
        }

        var fileInfo = new FileInfo(_options.FilePath);
        if (fileInfo.Length + nextWriteBytes <= _options.MaxFileSizeBytes)
        {
            return;
        }

        var rotatedPath = BuildRotatedPath(_options.FilePath);
        File.Move(_options.FilePath, rotatedPath);
        PruneRotatedFiles(_options.FilePath, _options.MaxRetainedFiles);
    }

    private static string BuildRotatedPath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");

        var candidate = Path.Combine(directory, $"{fileName}.{timestamp}{extension}");
        var index = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{fileName}.{timestamp}.{index}{extension}");
            index++;
        }

        return candidate;
    }

    private static void PruneRotatedFiles(string filePath, int maxRetainedFiles)
    {
        var directory = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var pattern = string.IsNullOrWhiteSpace(extension)
            ? $"{fileName}.*"
            : $"{fileName}.*{extension}";

        var rotatedFiles = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
            .ToArray();

        foreach (var stalePath in rotatedFiles.Skip(maxRetainedFiles))
        {
            File.Delete(stalePath);
        }
    }
}
